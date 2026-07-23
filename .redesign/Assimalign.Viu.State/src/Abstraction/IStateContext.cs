using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>Provides the dependencies and lifetime used to set up one state store.</summary>
public interface IStateContext
{
    /// <summary>Gets the store's detached reactive scope.</summary>
    IReactiveEffectScope Scope { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>Gets the component that first requested the store, or null for application setup.</summary>
    IComponentContext? Owner { get; }
}
