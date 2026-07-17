using Assimalign.Vue.Reactivity;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The runtime binding of <see cref="IWatchScheduler"/> ([V01.01.03.12], wiring the seam declared
/// by [V01.01.02.06]): a <see cref="WatchFlushMode.Pre"/> watcher routes into the scheduler's
/// pre-flush queue and a <see cref="WatchFlushMode.Post"/> watcher into the post-flush callbacks —
/// upstream <c>apiWatch.ts</c>: flush <c>'pre'</c> → <c>queueJob</c>, <c>'post'</c> →
/// <c>queuePostRenderEffect</c>. One instance serves one watcher (<see cref="VueWatch"/> creates a
/// fresh instance per watch), so the single <see cref="WatchJob"/> maps to exactly one
/// <see cref="SchedulerJob"/> and the scheduler's Queued-flag deduplication collapses repeated
/// triggers within a turn to a single delivery; a stopped watcher's queued job self-skips through
/// <see cref="WatchJob.Invoke"/>. A component-scoped watcher carries the owning instance's
/// <see cref="ComponentInstance.Uid"/> (upstream: <c>job.id = instance.uid</c>), so its pre-flush
/// callback runs after ancestors' renders but before its own component's re-render; an
/// instance-less pre watcher has no id and sorts ahead of every render (upstream <c>getId</c>).
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class RuntimeWatchScheduler : IWatchScheduler
{
    private readonly int? _identifier;
    private SchedulerJob? _schedulerJob;

    internal RuntimeWatchScheduler(int? identifier)
    {
        _identifier = identifier;
    }

    /// <inheritdoc />
    public void Schedule(WatchJob job)
    {
        _schedulerJob ??= new SchedulerJob(job.Invoke)
        {
            IsPreFlush = job.Flush == WatchFlushMode.Pre,
            Identifier = _identifier,
        };
        if (job.Flush == WatchFlushMode.Post)
        {
            Scheduler.QueuePostFlushCallback(_schedulerJob);
        }
        else
        {
            Scheduler.QueueJob(_schedulerJob);
        }
    }
}
