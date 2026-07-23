using System;
using System.Runtime.CompilerServices;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Routes reactive pre-flush and post-flush watcher jobs through the application scheduler.
/// </summary>
/// <remarks>
/// One instance may serve any number of watchers. Each <see cref="WatchJob"/> maps by reference to
/// one stable <see cref="SchedulerJob"/>, preserving queue deduplication across repeated
/// invalidations. The weak-key mapping does not retain stopped watchers. Not thread-safe by
/// design; Viu runs on the browser's single-threaded event loop.
/// </remarks>
public sealed class ApplicationWatchScheduler : IReactiveWatchScheduler
{
    private readonly ConditionalWeakTable<WatchJob, SchedulerJob> _schedulerJobs = new();
    private readonly int? _componentIdentifier;

    /// <summary>
    /// Creates an application-level scheduler whose watchers have no component ordering
    /// identifier.
    /// </summary>
    public ApplicationWatchScheduler()
    {
    }

    /// <summary>Creates a scheduler whose jobs order with one mounted component.</summary>
    /// <param name="componentIdentifier">The mounted component's scheduler identifier.</param>
    internal ApplicationWatchScheduler(int componentIdentifier)
    {
        _componentIdentifier = componentIdentifier;
    }

    /// <inheritdoc/>
    public void Schedule(WatchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.IsActive)
        {
            return;
        }

        SchedulerJob schedulerJob = _schedulerJobs.GetValue(job, CreateSchedulerJob);
        if (job.Flush == WatchFlushMode.Post)
        {
            Scheduler.QueuePostFlushCallback(schedulerJob);
        }
        else
        {
            Scheduler.QueueJob(schedulerJob);
        }
    }

    private SchedulerJob CreateSchedulerJob(WatchJob job)
    {
        return new SchedulerJob(job.Invoke)
        {
            Identifier = _componentIdentifier,
            IsPreFlush = job.Flush == WatchFlushMode.Pre,
            Name = "reactive watcher",
        };
    }
}
