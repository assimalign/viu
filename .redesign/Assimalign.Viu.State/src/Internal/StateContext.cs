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
        IComponentContext? owner)
    {
        Scope = scope;
        Components = components;
        Services = services;
        Owner = owner;
    }

    public IReactiveEffectScope Scope { get; }

    public IComponentFactory Components { get; }

    public IServiceProvider Services { get; }

    public IComponentContext? Owner { get; }
}
