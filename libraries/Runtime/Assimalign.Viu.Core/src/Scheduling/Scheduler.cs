using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// Coalesces application work into ordered pre-flush, render, and post-flush phases.
/// </summary>
/// <remarks>
/// The scheduler is ambient and single-threaded within one logical execution flow. Browser work
/// shares its JavaScript event-loop state; request-oriented hosts use isolated queues. Jobs queued
/// during one synchronous turn share one continuation. A per-flow gate prevents a thread-pool
/// fallback continuation from racing a synchronous renderer flush; callers still must not mutate
/// one graph from several threads. Specified by <c>[EXE-1]</c>, <c>[EXE-3]</c>, and <c>[SCH-1]</c>
/// through <c>[SCH-12]</c>.
/// </remarks>
public static class Scheduler
{
    private const int RecursionLimit = 100;

    private static SchedulerExecutionState State => CoreExecutionIsolation.Current.Scheduler;

    private static List<SchedulerJob> _queue => State.Queue;

    private static List<SchedulerJob> _pendingPostFlushCallbacks =>
        State.PendingPostFlushCallbacks;

    private static List<Action> _pendingHostCommits => State.PendingHostCommits;

    private static List<SchedulerJob>? _activePostFlushCallbacks
    {
        get => State.ActivePostFlushCallbacks;
        set => State.ActivePostFlushCallbacks = value;
    }

    private static List<SchedulerJob>? _executedInFlushChain
    {
        get => State.ExecutedInFlushChain;
        set => State.ExecutedInFlushChain = value;
    }

    private static Action<Action>? _flushDispatcher
    {
        get => State.FlushDispatcher;
        set => State.FlushDispatcher = value;
    }

    private static int _flushIndex
    {
        get => State.FlushIndex;
        set => State.FlushIndex = value;
    }

    private static bool _isFlushing
    {
        get => State.IsFlushing;
        set => State.IsFlushing = value;
    }

    private static bool _isFlushPending
    {
        get => State.IsFlushPending;
        set => State.IsFlushPending = value;
    }

    private static TaskCompletionSource? _flushCompletion
    {
        get => State.FlushCompletion;
        set => State.FlushCompletion = value;
    }

    private static long _nextInsertionSequence
    {
        get => State.NextInsertionSequence;
        set => State.NextInsertionSequence = value;
    }

    /// <summary>Gets whether a flush is executing.</summary>
    public static bool IsFlushing
    {
        get
        {
            SchedulerExecutionState state = State;
            lock (state.Synchronization)
            {
                return state.IsFlushing;
            }
        }
    }

    /// <summary>Gets whether a continuation has been scheduled for pending work.</summary>
    public static bool IsFlushPending
    {
        get
        {
            SchedulerExecutionState state = State;
            lock (state.Synchronization)
            {
                return state.IsFlushPending;
            }
        }
    }

    /// <summary>
    /// Installs a deterministic continuation dispatcher and returns a lease that restores the
    /// previous dispatcher. This seam exists only for host conformance tests and is not an
    /// application scheduling policy.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that receives each coalesced flush action.</param>
    /// <returns>A lease that restores the prior dispatcher when disposed in reverse installation order.</returns>
    /// <remarks>Specified by seam S2 in the component-model plan.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IDisposable UseFlushDispatcher(Action<Action> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            var registration = new FlushDispatcherRegistration(
                state,
                state.FlushDispatcher,
                dispatcher);
            state.FlushDispatcher = dispatcher;
            return registration;
        }
    }

    /// <summary>
    /// Clears pending jobs and flush bookkeeping so a deterministic host test starts from an empty
    /// scheduler. An installed dispatcher remains installed until its lease is disposed.
    /// </summary>
    /// <remarks>Test-host-only seam S2; production applications do not reset a live scheduler.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Reset()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            ClearQueuedFlag(_queue);
            ClearQueuedFlag(_pendingPostFlushCallbacks);
            if (_activePostFlushCallbacks is not null)
            {
                ClearQueuedFlag(_activePostFlushCallbacks);
            }

            _queue.Clear();
            _pendingPostFlushCallbacks.Clear();
            _pendingHostCommits.Clear();
            _activePostFlushCallbacks = null;
            _flushIndex = -1;
            _isFlushing = false;
            _isFlushPending = false;
            _nextInsertionSequence = 0;
            ResetExecutionCounters();
            _flushCompletion?.TrySetResult();
            _flushCompletion = null;
        }
    }

    /// <summary>
    /// Queues a job in order-key order. Repeated queues of the same instance before it runs are
    /// deduplicated. Specified by <c>[SCH-1]</c>, <c>[SCH-3]</c>, and <c>[SCH-5]</c>.
    /// </summary>
    /// <param name="job">The job to queue.</param>
    public static void QueueJob(SchedulerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            if ((job.Flags & SchedulerJobFlags.Queued) != 0)
            {
                return;
            }

            _queue.Insert(FindInsertionIndex(job.OrderKey), job);
            job.Flags |= SchedulerJobFlags.Queued;
            ScheduleFlush();
        }
    }

    /// <summary>
    /// Queues a callback for the lifecycle phase after render jobs and the first host commit.
    /// Equal order keys retain insertion order. Specified by <c>[SCH-2]</c> and <c>[SCH-4]</c>.
    /// </summary>
    /// <param name="callback">The callback to queue.</param>
    public static void QueuePostFlushCallback(SchedulerJob callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            if ((callback.Flags & SchedulerJobFlags.Queued) != 0)
            {
                return;
            }

            callback.InsertionSequence = _nextInsertionSequence++;
            _pendingPostFlushCallbacks.Add(callback);
            callback.Flags |= SchedulerJobFlags.Queued;
            ScheduleFlush();
        }
    }

    /// <summary>
    /// Returns a task that completes after the current or next flush chain, including work queued
    /// from post-flush callbacks. Specified by <c>[SCH-9]</c>.
    /// </summary>
    /// <returns>The current flush completion, or a completed task when no work is queued.</returns>
    public static Task NextTickAsync()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            return state.FlushCompletion?.Task ?? Task.CompletedTask;
        }
    }

    /// <summary>Runs pending pre-flush jobs immediately in order-key order.</summary>
    /// <remarks>Used by a synchronous renderer before it patches its tree.</remarks>
    public static void FlushPreFlushCallbacks()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            for (int index = _isFlushing ? _flushIndex + 1 : 0; index < _queue.Count; index++)
            {
                SchedulerJob job = _queue[index];
                if (!job.IsPreFlush)
                {
                    continue;
                }

                _queue.RemoveAt(index);
                index--;
                if (job.IsDisposed)
                {
                    job.Flags &= ~SchedulerJobFlags.Queued;
                    continue;
                }

                CheckRecursiveUpdates(job);
                if (job.AllowRecurse)
                {
                    job.Flags &= ~SchedulerJobFlags.Queued;
                }

                try
                {
                    job.Invoke();
                }
                finally
                {
                    job.Flags &= ~SchedulerJobFlags.Queued;
                }
            }
        }
    }

    /// <summary>
    /// Runs pending post-flush callbacks in stable order until the phase no longer queues another
    /// callback. Specified by <c>[SCH-2]</c>, <c>[SCH-4]</c>, and <c>[SCH-5]</c>.
    /// </summary>
    public static void FlushPostFlushCallbacks()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            if (_activePostFlushCallbacks is not null)
            {
                return;
            }

            while (_pendingPostFlushCallbacks.Count > 0)
            {
                var callbacks = new List<SchedulerJob>(_pendingPostFlushCallbacks);
                _pendingPostFlushCallbacks.Clear();
                callbacks.Sort(static (left, right) =>
                {
                    int orderComparison = left.OrderKey.CompareTo(right.OrderKey);
                    return orderComparison != 0
                        ? orderComparison
                        : left.InsertionSequence.CompareTo(right.InsertionSequence);
                });
                _activePostFlushCallbacks = callbacks;

                try
                {
                    for (int index = 0; index < callbacks.Count; index++)
                    {
                        SchedulerJob callback = callbacks[index];
                        if (callback.IsDisposed)
                        {
                            callback.Flags &= ~SchedulerJobFlags.Queued;
                            continue;
                        }

                        CheckRecursiveUpdates(callback);
                        if (callback.AllowRecurse)
                        {
                            callback.Flags &= ~SchedulerJobFlags.Queued;
                        }

                        callback.Invoke();
                        callback.Flags &= ~SchedulerJobFlags.Queued;
                    }
                }
                catch
                {
                    ClearQueuedFlag(callbacks);
                    throw;
                }
                finally
                {
                    _activePostFlushCallbacks = null;
                }
            }
        }
    }

    internal static void InvalidateJob(SchedulerJob job)
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            int index = _queue.IndexOf(job);
            if (index > _flushIndex)
            {
                _queue.RemoveAt(index);
                job.Flags &= ~SchedulerJobFlags.Queued;
            }
        }
    }

    internal static void QueueHostCommit(Action? commit)
    {
        if (commit is null)
        {
            return;
        }

        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            for (int index = 0; index < _pendingHostCommits.Count; index++)
            {
                if (ReferenceEquals(_pendingHostCommits[index], commit))
                {
                    return;
                }
            }

            _pendingHostCommits.Add(commit);
        }
    }

    internal static void FlushAfterSynchronousRender()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            if (_isFlushing)
            {
                return;
            }

            try
            {
                FlushPreFlushCallbacks();
                FlushHostCommits();
                FlushPostFlushCallbacks();
                FlushHostCommits();

                if (_isFlushPending && _queue.Count == 0 && _pendingPostFlushCallbacks.Count == 0)
                {
                    _isFlushPending = false;
                    CompleteFlushChain();
                }
            }
            catch
            {
                AbandonFlush();
                CompleteFlushChain();
                throw;
            }
        }
    }

    private static int FindInsertionIndex(long orderKey)
    {
        int low = _isFlushing ? _flushIndex + 1 : 0;
        int high = _queue.Count;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (_queue[middle].OrderKey <= orderKey)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static void ScheduleFlush()
    {
        if (_isFlushPending || _isFlushing)
        {
            return;
        }

        _isFlushPending = true;
        _flushCompletion ??= new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Action flush = CreateFlushAction();
        if (_flushDispatcher is not null)
        {
            _flushDispatcher(flush);
            return;
        }

        SynchronizationContext? context = SynchronizationContext.Current;
        if (context is not null)
        {
            context.Post(static state => ((Action)state!).Invoke(), flush);
            return;
        }

        ThreadPool.UnsafeQueueUserWorkItem(static state => ((Action)state!).Invoke(), flush);
    }

    private static Action CreateFlushAction()
    {
        ExecutionContext? executionContext = ExecutionContext.Capture();
        if (executionContext is null)
        {
            return FlushJobs;
        }

        return () => ExecutionContext.Run(
            executionContext.CreateCopy(),
            static _ => FlushJobs(),
            null);
    }

    private static void FlushJobs()
    {
        SchedulerExecutionState state = State;
        lock (state.Synchronization)
        {
            if (!_isFlushPending)
            {
                return;
            }

            _isFlushPending = false;
            _isFlushing = true;
            try
            {
                do
                {
                    for (_flushIndex = 0; _flushIndex < _queue.Count; _flushIndex++)
                    {
                        SchedulerJob job = _queue[_flushIndex];
                        if (job.IsDisposed)
                        {
                            job.Flags &= ~SchedulerJobFlags.Queued;
                            continue;
                        }

                        CheckRecursiveUpdates(job);
                        if (job.AllowRecurse)
                        {
                            job.Flags &= ~SchedulerJobFlags.Queued;
                        }

                        job.Invoke();
                        job.Flags &= ~SchedulerJobFlags.Queued;
                    }

                    _queue.Clear();
                    _flushIndex = -1;
                    FlushHostCommits();
                    FlushPostFlushCallbacks();
                    FlushHostCommits();
                }
                while (_queue.Count > 0 || _pendingPostFlushCallbacks.Count > 0);

                _isFlushing = false;
                CompleteFlushChain();
            }
            catch
            {
                AbandonFlush();
                _isFlushing = false;
                CompleteFlushChain();
                throw;
            }
        }
    }

    private static void AbandonFlush()
    {
        ClearQueuedFlag(_queue);
        ClearQueuedFlag(_pendingPostFlushCallbacks);
        if (_activePostFlushCallbacks is not null)
        {
            ClearQueuedFlag(_activePostFlushCallbacks);
        }

        _queue.Clear();
        _pendingPostFlushCallbacks.Clear();
        _pendingHostCommits.Clear();
        _activePostFlushCallbacks = null;
        _flushIndex = -1;
        _isFlushPending = false;
    }

    private static void ClearQueuedFlag(IReadOnlyList<SchedulerJob> jobs)
    {
        for (int index = 0; index < jobs.Count; index++)
        {
            jobs[index].Flags &= ~SchedulerJobFlags.Queued;
        }
    }

    private static void CompleteFlushChain()
    {
        ResetExecutionCounters();
        TaskCompletionSource? completion = _flushCompletion;
        _flushCompletion = null;
        completion?.TrySetResult();
    }

    private static void FlushHostCommits()
    {
        while (_pendingHostCommits.Count > 0)
        {
            Action[] commits = _pendingHostCommits.ToArray();
            _pendingHostCommits.Clear();
            for (int index = 0; index < commits.Length; index++)
            {
                commits[index]();
            }
        }
    }

    private static void CheckRecursiveUpdates(SchedulerJob job)
    {
        if (job.ExecutionsInCurrentFlushChain == 0)
        {
            (_executedInFlushChain ??= []).Add(job);
        }

        job.ExecutionsInCurrentFlushChain++;
        if (job.ExecutionsInCurrentFlushChain > RecursionLimit)
        {
            throw new InvalidOperationException(
                $"Maximum recursive updates exceeded{(job.Name is null ? string.Empty : $" in job '{job.Name}'")}" +
                $"{(job.Identifier is null ? string.Empty : $" (identifier {job.Identifier})")}. " +
                "A reactive effect is mutating its own dependencies while it runs.");
        }
    }

    private static void ResetExecutionCounters()
    {
        if (_executedInFlushChain is null)
        {
            return;
        }

        for (int index = 0; index < _executedInFlushChain.Count; index++)
        {
            _executedInFlushChain[index].ExecutionsInCurrentFlushChain = 0;
        }

        _executedInFlushChain = null;
    }

    private sealed class FlushDispatcherRegistration : IDisposable
    {
        private readonly SchedulerExecutionState _state;
        private readonly Action<Action>? _previous;
        private Action<Action>? _installed;

        internal FlushDispatcherRegistration(
            SchedulerExecutionState state,
            Action<Action>? previous,
            Action<Action> installed)
        {
            _state = state;
            _previous = previous;
            _installed = installed;
        }

        public void Dispose()
        {
            if (_installed is null)
            {
                return;
            }

            lock (_state.Synchronization)
            {
                if (!ReferenceEquals(_state.FlushDispatcher, _installed))
                {
                    throw new InvalidOperationException(
                        "Flush dispatcher leases must be disposed in reverse installation order.");
                }

                _state.FlushDispatcher = _previous;
                _installed = null;
            }
        }
    }
}
