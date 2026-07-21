using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// The batched job scheduler and <see cref="NextTick"/> — the C# port of
/// <c>@vue/runtime-core</c>'s scheduler (<c>packages/runtime-core/src/scheduler.ts</c>,
/// https://vuejs.org/api/general.html#nexttick). Jobs queued in one synchronous turn coalesce
/// into a single flush scheduled as a continuation on the current
/// <see cref="SynchronizationContext"/>: pre-flush jobs (watchers) run before render jobs in id
/// order (parents before children), and post-flush callbacks run after the queue drains.
/// All state is ambient and static — single-threaded JS event-loop model, NOT thread-safe.
/// When no <see cref="SynchronizationContext"/> is installed the flush falls back to the thread
/// pool, which on single-threaded browser WASM dispatches on the main thread via the JS event
/// loop; the interop-observable microtask-ordering guarantee is pinned by the WASM-hosted test
/// planned under [V01.01.03.04] (tests here install a pumped context instead).
/// </summary>
public static class Scheduler
{
    // Upstream RECURSION_LIMIT: how many times one job may execute within a single flush
    // chain before the scheduler reports an infinite update loop.
    private const int RecursionLimit = 100;

    private static readonly List<SchedulerJob> _queue = [];
    private static readonly List<SchedulerJob> _pendingPostFlushCallbacks = [];
    private static List<SchedulerJob>? _activePostFlushCallbacks;
    private static List<SchedulerJob>? _executedInFlushChain;
    private static int _flushIndex = -1;
    private static int _postFlushIndex;
    private static bool _isFlushing;
    private static bool _isFlushPending;
    private static TaskCompletionSource? _flushCompletion;
    private static long _nextInsertionSequence;

    /// <summary>
    /// Test seam: when set, scheduled flushes are handed to this dispatcher instead of the
    /// ambient <see cref="SynchronizationContext"/>, so tests can capture and run them
    /// deterministically on their own thread (see <c>Assimalign.Viu.Testing</c>'s pump).
    /// Production hosts leave this null.
    /// </summary>
    internal static Action<Action>? FlushDispatcher;

    /// <summary>
    /// Platform seam for <c>Assimalign.Viu.RuntimeDom</c>'s interop command buffer ([V01.01.04.05]):
    /// buffered node-ops accumulate DOM mutations and this seam is where the single batched interop
    /// call commits them. It fires at two boundaries within a flush, so it must be idempotent — a
    /// no-op when nothing is buffered:
    /// <list type="number">
    /// <item>after the pre/render job queue drains and <em>before</em> post-flush callbacks run, so
    /// the mounted/updated lifecycle phase (which may read the DOM — layout, template refs) observes
    /// the committed render; and</item>
    /// <item>again <em>after</em> post-flush callbacks run, because those hooks (and post-flush
    /// directive hooks such as <c>v-show</c>'s <c>updated</c>) can themselves write the DOM, and their
    /// buffered writes must commit within the same flush rather than strand until the next one.</item>
    /// </list>
    /// A steady-state render flush with no post-flush DOM writes therefore still crosses the boundary
    /// exactly once (the second call finds an empty buffer). Fires on both the scheduled flush and the
    /// synchronous post-render drain (<see cref="FlushAfterSynchronousRender"/>) so a direct
    /// <c>Render</c>/mount commits its batch too; a nested synchronous render inside an active
    /// scheduled flush is covered by the outer flush, never double-applied. Owner-managed like
    /// <see cref="FlushDispatcher"/> (armed by the buffered renderer, cleared on teardown); direct-mode
    /// hosts leave it null and pay nothing. Ambient static, single-threaded — NOT thread-safe.
    /// </summary>
    internal static Action? FlushBoundaryCallback;

    /// <summary>Whether a flush is executing right now.</summary>
    public static bool IsFlushing => _isFlushing;

    /// <summary>Whether a flush is scheduled but has not started.</summary>
    public static bool IsFlushPending => _isFlushPending;

    /// <summary>
    /// Queues <paramref name="job"/> for the next flush (upstream: <c>queueJob</c>). An
    /// already-queued job is deduplicated; a job queued during a flush is inserted into the
    /// running flush in id order. A job that queues itself while it is running is deduplicated
    /// away unless <see cref="SchedulerJob.AllowRecurse"/> is set.
    /// </summary>
    /// <param name="job">The job to queue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="job"/> is null.</exception>
    public static void QueueJob(SchedulerJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if ((job.Flags & SchedulerJobFlags.Queued) != 0)
        {
            return;
        }
        _queue.Insert(FindInsertionIndex(job.OrderKey), job);
        job.Flags |= SchedulerJobFlags.Queued;
        ScheduleFlush();
    }

    /// <summary>
    /// Queues <paramref name="callback"/> to run after the job queue drains (upstream:
    /// <c>queuePostFlushCb</c>) — the phase for mounted/updated lifecycle work.
    /// </summary>
    /// <param name="callback">The post-flush callback.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public static void QueuePostFlushCallback(SchedulerJob callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if ((callback.Flags & SchedulerJobFlags.Queued) != 0)
        {
            return;
        }
        callback.InsertionSequence = _nextInsertionSequence++;
        _pendingPostFlushCallbacks.Add(callback);
        callback.Flags |= SchedulerJobFlags.Queued;
        ScheduleFlush();
    }

    /// <summary>
    /// Returns a task that completes after the current or next flush — awaiting it observes
    /// post-patch state (upstream: <c>nextTick()</c>,
    /// https://vuejs.org/api/general.html#nexttick). Already completed when nothing is queued.
    /// </summary>
    public static Task NextTick()
        => _flushCompletion?.Task ?? Task.CompletedTask;

    /// <summary>
    /// Removes a not-yet-flushed job from the queue (upstream: <c>invalidateJob</c>) — a
    /// parent-driven component update runs the effect synchronously and must cancel the
    /// reactive update queued for the same instance.
    /// </summary>
    /// <param name="job">The job to remove.</param>
    internal static void InvalidateJob(SchedulerJob job)
    {
        var index = _queue.IndexOf(job);
        if (index > _flushIndex)
        {
            _queue.RemoveAt(index);
            job.Flags &= ~SchedulerJobFlags.Queued;
        }
    }

    /// <summary>
    /// Runs queued pre-flush jobs immediately, in queue order (upstream:
    /// <c>flushPreFlushCbs</c>) — the forced flush point before a synchronous render so
    /// watcher callbacks observe pre-patch state.
    /// </summary>
    public static void FlushPreFlushCallbacks()
    {
        for (var index = _isFlushing ? _flushIndex + 1 : 0; index < _queue.Count; index++)
        {
            var job = _queue[index];
            if (!job.IsPreFlush)
            {
                continue;
            }
            _queue.RemoveAt(index);
            index--;
            job.Flags &= ~SchedulerJobFlags.Queued;
            if (!job.IsDisposed)
            {
                job.Invoke();
            }
        }
    }

    /// <summary>
    /// Runs pending post-flush callbacks in id order until none remain (upstream:
    /// <c>flushPostFlushCbs</c>). Callbacks queued during the active pass fold into it,
    /// deduplicated.
    /// </summary>
    public static void FlushPostFlushCallbacks()
    {
        while (_pendingPostFlushCallbacks.Count > 0)
        {
            var callbacks = new List<SchedulerJob>(_pendingPostFlushCallbacks);
            _pendingPostFlushCallbacks.Clear();
            if (_activePostFlushCallbacks is not null)
            {
                // Re-entrant call while a pass is active: fold into the running pass.
                _activePostFlushCallbacks.AddRange(callbacks);
                return;
            }
            _activePostFlushCallbacks = callbacks;
            // Stable id ordering (JS sort is spec-stable; List<T>.Sort is not): equal-id
            // callbacks keep insertion order, which is what makes Mounted fire child-first.
            callbacks.Sort(static (left, right) =>
            {
                var byOrder = left.OrderKey.CompareTo(right.OrderKey);
                return byOrder != 0 ? byOrder : left.InsertionSequence.CompareTo(right.InsertionSequence);
            });
            try
            {
                for (_postFlushIndex = 0; _postFlushIndex < _activePostFlushCallbacks.Count; _postFlushIndex++)
                {
                    var callback = _activePostFlushCallbacks[_postFlushIndex];
                    callback.Flags &= ~SchedulerJobFlags.Queued;
                    if (!callback.IsDisposed)
                    {
                        callback.Invoke();
                    }
                }
            }
            finally
            {
                _activePostFlushCallbacks = null;
                _postFlushIndex = 0;
            }
        }
    }

    /// <summary>
    /// The synchronous drain a renderer performs after a direct (non-scheduled) render: pre-flush
    /// jobs, then post-flush callbacks, so lifecycle hooks fire before the render call returns
    /// (upstream: the flush pair in <c>render()</c>). No-op while a scheduled flush is running —
    /// that flush owns the drain.
    /// </summary>
    internal static void FlushAfterSynchronousRender()
    {
        if (_isFlushing)
        {
            return;
        }
        FlushPreFlushCallbacks();
        // Commit batched interop mutations from this synchronous render (mount / direct Render)
        // before its post-flush callbacks run and again after (they may write the DOM). See
        // FlushBoundaryCallback.
        FlushBoundaryCallback?.Invoke();
        FlushPostFlushCallbacks();
        FlushBoundaryCallback?.Invoke();
        if (_isFlushPending && _queue.Count == 0 && _pendingPostFlushCallbacks.Count == 0)
        {
            // This synchronous drain emptied everything a scheduled flush was posted for:
            // resolve NextTick now and let the stale post no-op on its pending guard.
            _isFlushPending = false;
            CompleteFlushChain();
        }
    }

    /// <summary>
    /// Test hook: clears every queue, counter, and flush flag so one test's leftovers cannot
    /// leak into the next. Ambient static state is the deliberate single-threaded design; tests
    /// reset it between cases.
    /// </summary>
    internal static void Reset()
    {
        foreach (var job in _queue)
        {
            job.Flags &= ~SchedulerJobFlags.Queued;
        }
        _queue.Clear();
        foreach (var callback in _pendingPostFlushCallbacks)
        {
            callback.Flags &= ~SchedulerJobFlags.Queued;
        }
        _pendingPostFlushCallbacks.Clear();
        _activePostFlushCallbacks = null;
        ResetExecutionCounters();
        _flushIndex = -1;
        _postFlushIndex = 0;
        _isFlushing = false;
        _isFlushPending = false;
        _flushCompletion?.TrySetResult();
        _flushCompletion = null;
    }

    private static int FindInsertionIndex(long orderKey)
    {
        // Binary search over the not-yet-flushed span; <= keeps first-queued-first order for
        // equal keys (upstream findInsertionIndex parity).
        var low = _isFlushing ? _flushIndex + 1 : 0;
        var high = _queue.Count;
        while (low < high)
        {
            var middle = (low + high) >> 1;
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
        _flushCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = FlushDispatcher;
        if (dispatcher is not null)
        {
            dispatcher(static () => FlushJobs());
            return;
        }
        var context = SynchronizationContext.Current;
        if (context is not null)
        {
            context.Post(static _ => FlushJobs(), null);
        }
        else
        {
            // No synchronization context: fall back to the thread pool. On single-threaded
            // browser WASM this still dispatches on the main thread through the JS event loop;
            // tests install the deterministic pump so this path never runs there (see class docs).
            ThreadPool.UnsafeQueueUserWorkItem(static _ => FlushJobs(), null);
        }
    }

    private static void FlushJobs()
    {
        if (!_isFlushPending)
        {
            // A stale post: the scheduler was reset (or already flushed) since scheduling.
            return;
        }
        _isFlushPending = false;
        _isFlushing = true;
        try
        {
            for (_flushIndex = 0; _flushIndex < _queue.Count; _flushIndex++)
            {
                var job = _queue[_flushIndex];
                if (job.IsDisposed)
                {
                    job.Flags &= ~SchedulerJobFlags.Queued;
                    continue;
                }
                CheckRecursiveUpdates(job);
                // ALLOW_RECURSE parity: clearing QUEUED before the run lets the job re-queue
                // itself; otherwise it stays flagged until after execution so self-queues
                // deduplicate away.
                if (job.AllowRecurse)
                {
                    job.Flags &= ~SchedulerJobFlags.Queued;
                }
                job.Invoke();
                job.Flags &= ~SchedulerJobFlags.Queued;
            }
            _queue.Clear();
            _flushIndex = -1;
            // Commit batched interop mutations before post-flush (mounted/updated) callbacks, which
            // may read the DOM, and again after them, since those hooks may write it. See
            // FlushBoundaryCallback.
            FlushBoundaryCallback?.Invoke();
            FlushPostFlushCallbacks();
            FlushBoundaryCallback?.Invoke();
            _isFlushing = false;
            if (_queue.Count > 0 || _pendingPostFlushCallbacks.Count > 0)
            {
                // Post-flush callbacks queued more work: run another cycle before NextTick
                // resolves, sharing this chain's recursion bookkeeping (upstream parity:
                // flushJobs re-invokes itself with the same `seen` map).
                _isFlushPending = true;
                FlushJobs();
                return;
            }
            CompleteFlushChain();
        }
        catch
        {
            // Abandon the remaining queue deterministically: clear queued flags so every job
            // can re-queue, resolve NextTick so awaiters do not hang, and let the exception
            // reach the host. App-level error routing lands with [V01.01.03.12].
            foreach (var job in _queue)
            {
                job.Flags &= ~SchedulerJobFlags.Queued;
            }
            _queue.Clear();
            _flushIndex = -1;
            _isFlushing = false;
            CompleteFlushChain();
            throw;
        }
    }

    private static void CompleteFlushChain()
    {
        ResetExecutionCounters();
        var completion = _flushCompletion;
        _flushCompletion = null;
        completion?.TrySetResult();
    }

    private static void ResetExecutionCounters()
    {
        if (_executedInFlushChain is null)
        {
            return;
        }
        foreach (var job in _executedInFlushChain)
        {
            job.ExecutionsInCurrentFlushCycle = 0;
        }
        _executedInFlushChain = null;
    }

    private static void CheckRecursiveUpdates(SchedulerJob job)
    {
        if (job.ExecutionsInCurrentFlushCycle == 0)
        {
            (_executedInFlushChain ??= []).Add(job);
        }
        job.ExecutionsInCurrentFlushCycle++;
        if (job.ExecutionsInCurrentFlushCycle > RecursionLimit)
        {
            throw new InvalidOperationException(
                $"Maximum recursive updates exceeded{(job.Name is null ? string.Empty : $" in job '{job.Name}'")}"
                + $"{(job.Identifier is null ? string.Empty : $" (id {job.Identifier})")}. "
                + "This means a reactive effect is mutating its own dependencies and thus recursively "
                + "triggering itself. (Upstream parity: scheduler.ts checkRecursiveUpdates.)");
        }
    }
}
