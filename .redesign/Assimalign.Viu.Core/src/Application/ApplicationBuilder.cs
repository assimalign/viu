using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// The default application builder. It accepts an already-composed component factory and never
/// constructs or owns a dependency-injection container.
/// </summary>
public sealed class ApplicationBuilder : IApplicationBuilder
{
    private IComponent? _rootComponent;
    private IComponentFactory? _components;
    private IStateStoreRegistry? _state;

    /// <inheritdoc/>
    public IApplicationBuilder UseRootComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _rootComponent = component;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder UseComponentFactory(IComponentFactory components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder UseStateRegistry(IStateStoreRegistry state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        return this;
    }

    /// <summary>
    /// Builds the application without taking ownership of the supplied factory, provider, or state
    /// registry.
    /// </summary>
    /// <returns>The configured application.</returns>
    public IApplication Build()
    {
        IComponent rootComponent = _rootComponent
            ?? throw new InvalidOperationException("Configure a root component before building the application.");
        IComponentFactory components = _components
            ?? throw new InvalidOperationException("Configure a component factory before building the application.");
        return new Application(new ApplicationContext(rootComponent, components, _state));
    }
}

