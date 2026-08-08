using System;
using System.Collections.Generic;

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
    private bool _isDisposed;

    private TestSchedulerPump()
    {
        _registration = Scheduler.UseFlushDispatcher(_pendingFlushes.Enqueue);
    }

    /// <summary>Gets the number of captured flush continuations waiting to execute.</summary>
    public int PendingFlushCount => _pendingFlushes.Count;

    /// <summary>Installs a deterministic dispatcher and returns its restoration lease.</summary>
    /// <returns>The installed scheduler pump.</returns>
    public static TestSchedulerPump Install() => new();

    /// <summary>Runs captured continuations, including continuations captured while draining.</summary>
    /// <returns>The number of continuations executed.</returns>
    public int RunUntilIdle()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
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
}
