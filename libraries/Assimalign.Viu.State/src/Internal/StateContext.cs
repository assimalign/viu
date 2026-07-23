using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateContext : IStateContext
{
    internal StateContext(
        IReactiveEffectScope scope,
        IComponentFactory components,
        IServiceProvider services,
        IReactiveWatchScheduler? watchScheduler,
        IComponentContext? owner)
    {
        Scope = scope;
        Components = components;
        Services = services;
        WatchScheduler = watchScheduler;
        Owner = owner;
    }

    public IReactiveEffectScope Scope { get; }

    public IComponentFactory Components { get; }

    public IServiceProvider Services { get; }

    public IReactiveWatchScheduler? WatchScheduler { get; }

    public IComponentContext? Owner { get; }
}
