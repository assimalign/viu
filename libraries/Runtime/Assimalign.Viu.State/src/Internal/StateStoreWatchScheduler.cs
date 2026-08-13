using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreWatchScheduler : IReactiveWatchScheduler
{
    private readonly Action _onScheduled;
    private readonly IReactiveWatchScheduler _scheduler;

    internal StateStoreWatchScheduler(
        IReactiveWatchScheduler scheduler,
        Action onScheduled)
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
