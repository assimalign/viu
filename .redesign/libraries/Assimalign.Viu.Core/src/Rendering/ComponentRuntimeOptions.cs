using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Supplies externally owned composition objects used by the component host.
/// </summary>
/// <remarks>
/// Deliberately absent: any per-convention composition such as a state-store registry.
/// Conventions reach a mounted component only through <see cref="Services"/> or their own ambient
/// registration; they never earn an option or a context member.
/// </remarks>
public sealed class ComponentRuntimeOptions
{
    /// <summary>Initializes host composition.</summary>
    /// <param name="components">The registration-backed component factory.</param>
    /// <param name="watchScheduler">The component watch scheduling policy.</param>
    /// <param name="services">The optional externally owned application service provider.</param>
    public ComponentRuntimeOptions(
        IComponentFactory components,
        IReactiveWatchScheduler watchScheduler,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(watchScheduler);
        Components = components;
        WatchScheduler = watchScheduler;
        Services = services;
    }

    /// <summary>Gets the borrowed component factory.</summary>
    public IComponentFactory Components { get; }

    /// <summary>Gets the borrowed component watch scheduler.</summary>
    public IReactiveWatchScheduler WatchScheduler { get; }

    /// <summary>Gets the borrowed optional application service provider.</summary>
    public IServiceProvider? Services { get; }
}
