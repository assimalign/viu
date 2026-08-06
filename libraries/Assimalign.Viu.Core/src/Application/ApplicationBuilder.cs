using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// The shared application-composition builder base. It accepts an already-composed component
/// factory and never constructs or owns a dependency-injection container.
/// </summary>
/// <remarks>
/// Concrete persistent-host and server-render builders override the fluent methods with covariant
/// return types so composition never erases the derived builder. Specified by <c>[APP-2]</c>.
/// </remarks>
public abstract class ApplicationBuilder
{
    private readonly ApplicationOptions _options = new();
    private IComponent? _rootComponent;
    private IComponentFactory _components = EmptyComponentFactory.Instance;
    private IServiceProvider _services = EmptyServiceProvider.Instance;
    private IStateStoreRegistry? _state;
    private IDirectiveResolver? _directives;

    /// <summary>Sets the root value in the component tree.</summary>
    /// <param name="component">The root component.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder AddRootComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _rootComponent = component;
        return this;
    }

    /// <summary>Sets the application-selected component resolver.</summary>
    /// <param name="components">The application component factory.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder AddComponentFactory(IComponentFactory components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
        return this;
    }

    /// <summary>Attaches an independently supplied application service resolver.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder AddServiceProvider(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        return this;
    }

    /// <summary>Sets the optional application state registry.</summary>
    /// <param name="state">The application state registry.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder AddStateRegistry(IStateStoreRegistry state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        return this;
    }

    /// <summary>Sets the optional application directive resolver.</summary>
    /// <param name="directives">The application directive resolver.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder AddDirectiveResolver(IDirectiveResolver directives)
    {
        ArgumentNullException.ThrowIfNull(directives);
        _directives = directives;
        return this;
    }

    /// <summary>Configures diagnostics that are frozen when an application is built.</summary>
    /// <param name="configure">The application-options action.</param>
    /// <returns>This builder.</returns>
    public virtual ApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    /// <summary>Creates the validated immutable context for the derived composition object.</summary>
    /// <param name="decorateComponents">
    /// An optional derived-builder decorator for the application-selected component factory.
    /// </param>
    /// <returns>The application context.</returns>
    protected IApplicationContext CreateContext(
        Func<IComponentFactory, IComponentFactory>? decorateComponents = null)
    {
        IComponent rootComponent = _rootComponent
            ?? throw new InvalidOperationException("Configure a root component before building the application.");
        IComponentFactory components = decorateComponents is null
            ? _components
            : decorateComponents(_components)
                ?? throw new InvalidOperationException(
                    "The component-factory decorator returned null.");
        return new ApplicationContext(
            rootComponent,
            components,
            _services,
            _state,
            _directives,
            _options);
    }
}
