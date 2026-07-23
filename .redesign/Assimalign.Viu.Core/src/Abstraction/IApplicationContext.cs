using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Provides the root component, independently selected component and service resolvers, and optional
/// state registry shared by one application.
/// </summary>
public interface IApplicationContext
{
    /// <summary>Gets the root value in the unified component tree.</summary>
    IComponent RootComponent { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>Gets the optional application state registry.</summary>
    IStateStoreRegistry? State { get; }
}
