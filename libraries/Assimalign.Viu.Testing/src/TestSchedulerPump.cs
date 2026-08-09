using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>Captures scheduler continuations and executes them deterministically on demand.</summary>
/// <remarks>
/// Installation uses the public test-host lease and is single-threaded. Specified by seam S2 in
/// the component-model plan and by <c>[SCH-1]</c> through <c>[SCH-12]</c>.
/// </remarks>
public sealed class TestSchedulerPump : IDisposable
{
    private readonly Queue<Action> _pendingFlushes = [];
    private readonly IDisposable _registration;
    private readonly TestSynchronizationContext? _synchronizationContext;
    private bool _isDisposed;
    private int _pendingContextFlushCount;
    private int _executedContextFlushCount;

    private TestSchedulerPump(TestSynchronizationContext? synchronizationContext)
    {
        _synchronizationContext = synchronizationContext;
        _registration = Scheduler.UseFlushDispatcher(
            synchronizationContext is null
                ? _pendingFlushes.Enqueue
                : QueueOnSynchronizationContext);
    }

    /// <summary>Gets the number of captured flush continuations waiting to execute.</summary>
    public int PendingFlushCount => _synchronizationContext is null
        ? _pendingFlushes.Count
        : Volatile.Read(ref _pendingContextFlushCount);

    /// <summary>Installs a deterministic dispatcher and returns its restoration lease.</summary>
    /// <returns>The installed scheduler pump.</returns>
    public static TestSchedulerPump Install() => new(synchronizationContext: null);

    /// <summary>
    /// Installs a deterministic dispatcher that shares one ordered queue with asynchronous test
    /// continuations.
    /// </summary>
    /// <param name="synchronizationContext">The test-owned continuation queue.</param>
    /// <returns>The installed scheduler pump.</returns>
    public static TestSchedulerPump Install(
        TestSynchronizationContext synchronizationContext)
    {
        ArgumentNullException.ThrowIfNull(synchronizationContext);
        return new TestSchedulerPump(synchronizationContext);
    }

    /// <summary>
    /// Drains the installed queue, including scheduler and asynchronous continuations captured
    /// while draining.
    /// </summary>
    /// <returns>The number of captured scheduler flush continuations executed.</returns>
    public int RunUntilIdle()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_synchronizationContext is not null)
        {
            int previousExecuted = Volatile.Read(ref _executedContextFlushCount);
            _synchronizationContext.Drain();
            return Volatile.Read(ref _executedContextFlushCount) - previousExecuted;
        }

        int executed = 0;
        while (_pendingFlushes.Count > 0)
        {
            _pendingFlushes.Dequeue()();
            executed++;
        }

        return executed;
    }

    /// <summary>Restores the scheduler dispatcher that preceded this lease.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _registration.Dispose();
        _pendingFlushes.Clear();
        _isDisposed = true;
    }

    private void QueueOnSynchronizationContext(Action continuation)
    {
        Interlocked.Increment(ref _pendingContextFlushCount);
        _synchronizationContext!.Post(
            static state =>
            {
                var dispatch = (ContextDispatch)state!;
                Interlocked.Decrement(ref dispatch.Owner._pendingContextFlushCount);
                Interlocked.Increment(ref dispatch.Owner._executedContextFlushCount);
                dispatch.Continuation();
            },
            new ContextDispatch(this, continuation));
    }

    private sealed record ContextDispatch(TestSchedulerPump Owner, Action Continuation);
}
