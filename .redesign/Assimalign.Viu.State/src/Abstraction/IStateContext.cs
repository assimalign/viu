using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>Provides the dependencies and lifetime used to set up one state store.</summary>
public interface IStateContext
{
    /// <summary>Gets the store's detached reactive scope.</summary>
    IReactiveScope Scope { get; }

    /// <summary>Gets the shared component activator and dependency resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the standard .NET service resolver.</summary>
    IServiceProvider Services => Components;

    /// <summary>Gets the component that first requested the store, or null for application setup.</summary>
    IComponentContext? Owner { get; }
}

