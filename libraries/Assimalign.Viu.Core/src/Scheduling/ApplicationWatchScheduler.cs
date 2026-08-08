using System;
using System.Runtime.CompilerServices;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Routes component watcher jobs through the application scheduler's pre- and post-flush phases.
/// </summary>
/// <remarks>
/// Each reactive job maps by reference to one stable scheduler job, preserving instance
/// deduplication. This scheduler is single-threaded. Specified by <c>[SCH-2]</c>, <c>[SCH-3]</c>,
/// and <c>[SCH-5]</c>.
/// </remarks>
public sealed class ApplicationWatchScheduler : IReactiveWatchScheduler
{
    private readonly ConditionalWeakTable<WatchJob, SchedulerJob> _schedulerJobs = new();
    private readonly int? _componentIdentifier;

    /// <summary>Initializes an application-level scheduler with no component ordering identifier.</summary>
    public ApplicationWatchScheduler()
    {
    }

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

    private SchedulerJob CreateSchedulerJob(WatchJob job) =>
        new(job.Invoke)
        {
            Identifier = _componentIdentifier,
            IsPreFlush = job.Flush == WatchFlushMode.Pre,
            Name = "reactive watcher",
        };
}
