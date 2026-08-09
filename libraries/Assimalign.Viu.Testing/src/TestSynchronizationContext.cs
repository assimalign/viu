using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Queues asynchronous continuations for deterministic execution on one active test thread.
/// </summary>
/// <remarks>
/// This context never waits on a wall clock or delegates a continuation to the thread pool. Tests
/// explicitly drain queued work or pump an asynchronous operation; disposal reports forgotten work
/// instead of allowing a test to hang. Instances are not thread-safe for execution, but
/// <see cref="Post"/> safely accepts a continuation from a completing task on another thread.
/// Specified by <c>[V01.01.11.05]</c>.
/// </remarks>
public sealed class TestSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly Queue<Continuation> _continuations = [];
    private readonly object _synchronization = new();
    private readonly int _installationThreadIdentifier;
    private readonly SynchronizationContext? _rootPreviousContext;
    private readonly bool _ownsRootInstallation;
    private bool _isDisposed;
    private bool _isDraining;
    private int _pendingOperationCount;

    private TestSynchronizationContext(bool install)
    {
        _installationThreadIdentifier = Environment.CurrentManagedThreadId;
        if (install)
        {
            _rootPreviousContext = Current;
            _ownsRootInstallation = true;
            SetSynchronizationContext(this);
        }
    }

    /// <summary>Gets the number of queued continuations waiting for an explicit drain.</summary>
    public int PendingContinuationCount
    {
        get
        {
            lock (_synchronization)
            {
                return _continuations.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of incomplete asynchronous-void operations registered with this context.
    /// </summary>
    public int PendingOperationCount
    {
        get
        {
            lock (_synchronization)
            {
                return _pendingOperationCount;
            }
        }
    }

    /// <summary>Installs a deterministic context on the current thread.</summary>
    /// <returns>The installed context, whose disposal restores the preceding context.</returns>
    public static TestSynchronizationContext Install() => new(install: true);

    /// <summary>Queues a continuation without running it reentrantly.</summary>
    /// <param name="callback">The continuation callback.</param>
    /// <param name="state">The callback state.</param>
    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_synchronization)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _continuations.Enqueue(new Continuation(callback, state));
        }
    }

    /// <summary>
    /// Runs a synchronous callback on the active test thread and rejects invalid blocking sends.
    /// </summary>
    /// <param name="callback">The callback to run.</param>
    /// <param name="state">The callback state.</param>
    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfUnavailable();
        if (!ReferenceEquals(Current, this))
        {
            throw new InvalidOperationException(
                "A deterministic synchronization-context send can run only while that context "
                + "is active. Use Run, Drain, or Pump instead of blocking another thread.");
        }

        callback(state);
    }

    /// <summary>Returns this single logical context rather than creating an independent queue.</summary>
    /// <returns>This context.</returns>
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>Records an asynchronous-void operation for disposal diagnostics.</summary>
    public override void OperationStarted()
    {
        lock (_synchronization)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _pendingOperationCount++;
        }
    }

    /// <summary>Marks one asynchronous-void operation complete.</summary>
    public override void OperationCompleted()
    {
        lock (_synchronization)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_pendingOperationCount == 0)
            {
                throw new InvalidOperationException(
                    "The deterministic synchronization context has no pending operation to complete.");
            }

            _pendingOperationCount--;
        }
    }

    /// <summary>
    /// Runs queued continuations in first-in, first-out order, including work posted while draining.
    /// </summary>
    /// <returns>The number of continuations executed.</returns>
    public int Drain()
    {
        ThrowIfUnavailable();
        lock (_synchronization)
        {
            if (_isDraining)
            {
                throw new InvalidOperationException(
                    "The deterministic synchronization context is already draining. "
                    + "Post nested work and let the active drain execute it in order.");
            }

            _isDraining = true;
        }

        int executed = 0;
        using IDisposable registration = Enter();
        try
        {
            while (TryTake(out Continuation continuation))
            {
                continuation.Callback(continuation.State);
                executed++;
            }
        }
        finally
        {
            lock (_synchronization)
            {
                _isDraining = false;
            }
        }

        return executed;
    }

    /// <summary>Pumps queued continuations until an asynchronous operation completes.</summary>
    /// <param name="operation">The operation to complete without a wall-clock wait.</param>
    public void Pump(Task operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfUnavailable();
        while (!operation.IsCompleted)
        {
            int executed = Drain();
            if (!operation.IsCompleted && executed == 0)
            {
                throw CannotProgress();
            }
        }

        operation.GetAwaiter().GetResult();
    }

    /// <summary>Pumps queued continuations until a value-producing operation completes.</summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="operation">The operation to complete without a wall-clock wait.</param>
    /// <returns>The completed operation's result.</returns>
    public TResult Pump<TResult>(Task<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Pump((Task)operation);
        return operation.GetAwaiter().GetResult();
    }

    /// <summary>Runs a synchronous action under this context and then drains posted work.</summary>
    /// <param name="action">The action to run on the active test thread.</param>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfUnavailable();
        using (Enter())
        {
            action();
        }

        Drain();
    }

    /// <summary>Runs an asynchronous action and pumps it to completion deterministically.</summary>
    /// <param name="action">The action to start on the active test thread.</param>
    public void Run(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfUnavailable();
        Task operation;
        using (Enter())
        {
            operation = action()
                ?? throw new InvalidOperationException(
                    "The asynchronous test action returned a null task.");
        }

        Pump(operation);
        Drain();
    }

    /// <summary>Restores the preceding context and reports any forgotten asynchronous work.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        ThrowIfWrongThread();
        if (_ownsRootInstallation)
        {
            if (!ReferenceEquals(Current, this))
            {
                throw new InvalidOperationException(
                    "Synchronization context leases must be disposed in reverse installation order.");
            }

            SetSynchronizationContext(_rootPreviousContext);
        }

        int pendingContinuations;
        int pendingOperations;
        lock (_synchronization)
        {
            pendingContinuations = _continuations.Count;
            pendingOperations = _pendingOperationCount;
            _isDisposed = true;
        }

        if (pendingContinuations > 0 || pendingOperations > 0)
        {
            throw new InvalidOperationException(
                "The deterministic synchronization context was disposed with "
                + $"{pendingContinuations} queued continuation(s) and "
                + $"{pendingOperations} incomplete asynchronous operation(s). "
                + "Call Drain, Pump, or Run before disposal so no component continuation is forgotten.");
        }
    }

    internal static TestSynchronizationContext CreateDetached() => new(install: false);

    internal IDisposable Enter()
    {
        ThrowIfUnavailable();
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(this);
        return new ContextRegistration(this, previous);
    }

    internal TResult Run<TResult>(Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfUnavailable();
        TResult result;
        using (Enter())
        {
            result = action();
        }

        Drain();
        return result;
    }

    private bool TryTake(out Continuation continuation)
    {
        lock (_synchronization)
        {
            if (_continuations.Count == 0)
            {
                continuation = default;
                return false;
            }

            continuation = _continuations.Dequeue();
            return true;
        }
    }

    private void ThrowIfUnavailable()
    {
        ThrowIfWrongThread();
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void ThrowIfWrongThread()
    {
        if (_ownsRootInstallation
            && Environment.CurrentManagedThreadId != _installationThreadIdentifier)
        {
            throw new InvalidOperationException(
                "An installed deterministic synchronization context must be restored on its "
                + "installation thread. Use TestRenderer's scoped context for async tests whose "
                + "logical test flow can resume on another physical thread.");
        }
    }

    private static InvalidOperationException CannotProgress() =>
        new(
            "The asynchronous test operation is incomplete, but the deterministic "
            + "synchronization context has no queued continuation to run. Complete the awaited "
            + "source under test control or post its continuation before calling Pump; "
            + "the test host does not wait on wall-clock or thread-pool timing.");

    private readonly record struct Continuation(SendOrPostCallback Callback, object? State);

    private sealed class ContextRegistration : IDisposable
    {
        private readonly TestSynchronizationContext _owner;
        private readonly SynchronizationContext? _previous;
        private readonly int _installationThreadIdentifier;
        private bool _isDisposed;

        internal ContextRegistration(
            TestSynchronizationContext owner,
            SynchronizationContext? previous)
        {
            _owner = owner;
            _previous = previous;
            _installationThreadIdentifier = Environment.CurrentManagedThreadId;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (Environment.CurrentManagedThreadId != _installationThreadIdentifier)
            {
                throw new InvalidOperationException(
                    "A scoped synchronization context must be restored on the thread where it "
                    + "was installed.");
            }

            if (!ReferenceEquals(Current, _owner))
            {
                throw new InvalidOperationException(
                    "Synchronization context leases must be disposed in reverse installation order.");
            }

            SetSynchronizationContext(_previous);
            _isDisposed = true;
        }
    }
}
