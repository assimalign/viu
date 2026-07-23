using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// The host-neutral application builder base. It accepts an already-composed component factory and
/// never constructs or owns a dependency-injection container.
/// </summary>
public abstract class ApplicationBuilder : IApplicationBuilder
{
    private readonly List<Action<IApplication>> _configuration = [];
    private IComponent? _rootComponent;
    private IComponentFactory? _components;
    private IServiceProvider? _services;
    private IStateStoreRegistry? _state;
    private IDirectiveResolver? _directives;

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
    public IApplicationBuilder UseServiceProvider(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder UseStateRegistry(IStateStoreRegistry state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder UseDirectiveResolver(IDirectiveResolver directives)
    {
        ArgumentNullException.ThrowIfNull(directives);
        _directives = directives;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder Use(IApplicationPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _configuration.Add(application => application.Use(plugin));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder ConfigureApplication(Action<IApplicationContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configuration.Add(application => configure(application.Context));
        return this;
    }

    /// <summary>
    /// Builds the host-specific application without taking ownership of the supplied factory,
    /// provider, or state registry.
    /// </summary>
    /// <returns>The configured application.</returns>
    public abstract IApplication Build();

    /// <summary>Creates the validated immutable context for a host-specific application.</summary>
    /// <returns>The application context.</returns>
    protected IApplicationContext CreateContext()
    {
        IComponent rootComponent = _rootComponent
            ?? throw new InvalidOperationException("Configure a root component before building the application.");
        IComponentFactory components = _components
            ?? throw new InvalidOperationException("Configure a component factory before building the application.");
        IServiceProvider services = _services
            ?? throw new InvalidOperationException("Configure a service provider before building the application.");
        return new ApplicationContext(
            rootComponent,
            components,
            services,
            _state,
            _directives);
    }

    /// <summary>Applies recorded plugins and context configuration in call order.</summary>
    /// <param name="application">The newly created host application.</param>
    protected void ApplyConfiguration(IApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        foreach (Action<IApplication> configure in _configuration)
        {
            configure(application);
        }
    }
}
