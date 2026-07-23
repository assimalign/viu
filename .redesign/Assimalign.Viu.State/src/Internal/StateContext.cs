using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateContext : IStateContext
{
    internal StateContext(
        IReactiveScope scope,
        IComponentFactory components,
        IComponentContext? owner)
    {
        Scope = scope;
        Components = components;
        Owner = owner;
    }

    public IReactiveScope Scope { get; }

    public IComponentFactory Components { get; }

    public IComponentContext? Owner { get; }
}

