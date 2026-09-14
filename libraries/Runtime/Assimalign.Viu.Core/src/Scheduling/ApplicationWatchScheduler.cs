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
/// and <c>[SCH-5]</c>. Renderer-created instances retain scheduled post-flush reactions for a
/// hidden Suspense generation until it is revealed, as specified by <c>[BLT-13]</c>.
/// </remarks>
public sealed class ApplicationWatchScheduler : IReactiveWatchScheduler
{
    private readonly ConditionalWeakTable<WatchJob, SchedulerJob> _schedulerJobs = new();
    private readonly int? _componentIdentifier;
    private readonly SuspenseBoundary? _suspenseBoundary;

    /// <summary>Initializes an application-level scheduler with no component ordering identifier.</summary>
    public ApplicationWatchScheduler()
    {
    }

    internal ApplicationWatchScheduler(
        int componentIdentifier,
        SuspenseBoundary? suspenseBoundary = null)
    {
        _componentIdentifier = componentIdentifier;
        _suspenseBoundary = suspenseBoundary;
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
            if (_suspenseBoundary is not null)
            {
                _suspenseBoundary.QueueEffect(schedulerJob);
            }
            else
            {
                Scheduler.QueuePostFlushCallback(schedulerJob);
            }
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
