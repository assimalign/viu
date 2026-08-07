using System;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateContext : IStateContext
{
    internal StateContext(
        EffectScope scope,
        IServiceProvider? services,
        IReactiveWatchScheduler watchScheduler)
    {
        Scope = scope;
        Services = services;
        WatchScheduler = watchScheduler;
    }

    public EffectScope Scope { get; }

    public IServiceProvider? Services { get; }

    public IReactiveWatchScheduler WatchScheduler { get; }
}
