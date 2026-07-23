using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>
/// Observes when a state-store watch job has actually been scheduled, then delegates application
/// flush ownership to the configured Reactivity scheduler.
/// </summary>
internal sealed class StateStoreWatchScheduler : IReactiveWatchScheduler
{
    private readonly IReactiveWatchScheduler _scheduler;
    private readonly Action _onScheduled;

    internal StateStoreWatchScheduler(IReactiveWatchScheduler scheduler, Action onScheduled)
    {
        _scheduler = scheduler;
        _onScheduled = onScheduled;
    }

    public void Schedule(WatchJob job)
    {
        _onScheduled();
        _scheduler.Schedule(job);
    }
}
