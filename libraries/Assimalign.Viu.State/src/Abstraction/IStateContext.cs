using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

/// <summary>Provides the dependencies and lifetime used to set up one state store.</summary>
public interface IStateContext
{
    /// <summary>Gets the state store's reactive scope.</summary>
    /// <remarks>
    /// The scope is a child of the registry's detached root scope. It is therefore isolated from
    /// the component scope that first resolves the store while still being stopped with the
    /// registry.
    /// </remarks>
    IReactiveEffectScope Scope { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the application watch scheduler, or <see langword="null"/> when watches should use
    /// standalone Reactivity's synchronous fallback.
    /// </summary>
    IReactiveWatchScheduler? WatchScheduler { get; }

    /// <summary>Gets the component that first requested the store, or null for application setup.</summary>
    IComponentContext? Owner { get; }
}
