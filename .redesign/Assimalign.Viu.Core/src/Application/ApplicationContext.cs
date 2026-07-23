using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>The immutable default application composition context.</summary>
public sealed class ApplicationContext : IApplicationContext
{
    /// <summary>Creates an application context.</summary>
    /// <param name="rootComponent">The root value in the component tree.</param>
    /// <param name="components">The combined component activator and dependency resolver.</param>
    /// <param name="state">The optional application state registry.</param>
    public ApplicationContext(
        IComponent rootComponent,
        IComponentFactory components,
        IStateStoreRegistry? state = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);
        RootComponent = rootComponent;
        Components = components;
        State = state;
    }

    /// <inheritdoc/>
    public IComponent RootComponent { get; }

    /// <inheritdoc/>
    public IComponentFactory Components { get; }

    /// <inheritdoc/>
    public IStateStoreRegistry? State { get; }
}

