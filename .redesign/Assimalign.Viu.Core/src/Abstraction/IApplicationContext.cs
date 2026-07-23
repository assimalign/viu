using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Provides the root component, component factory, standard service resolver, and optional state
/// registry shared by one application.
/// </summary>
public interface IApplicationContext
{
    /// <summary>Gets the root value in the unified component tree.</summary>
    IComponent RootComponent { get; }

    /// <summary>Gets the combined component activator and dependency resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>
    /// Gets the standard .NET service resolver. This is the same object as <see cref="Components"/>.
    /// </summary>
    IServiceProvider Services => Components;

    /// <summary>Gets the optional application state registry.</summary>
    IStateStoreRegistry? State { get; }
}

