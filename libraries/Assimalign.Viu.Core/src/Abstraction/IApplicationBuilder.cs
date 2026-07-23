using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Composes a Viu application from a root component, independently supplied component and service
/// resolvers, and an optional state registry.
/// </summary>
public interface IApplicationBuilder
{
    /// <summary>Sets the root value in the component tree.</summary>
    /// <param name="component">The root component.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseRootComponent(IComponent component);

    /// <summary>Sets the application-selected component resolver.</summary>
    /// <param name="components">The application component factory.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseComponentFactory(IComponentFactory components);

    /// <summary>Sets the independently supplied application service resolver.</summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseServiceProvider(IServiceProvider services);

    /// <summary>Sets the optional application state registry.</summary>
    /// <param name="state">The application state registry.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseStateRegistry(IStateStoreRegistry state);

    /// <summary>Sets the optional application directive resolver.</summary>
    /// <param name="directives">The application directive resolver.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder UseDirectiveResolver(IDirectiveResolver directives);

    /// <summary>Records a platform-neutral plugin for installation before the first render.</summary>
    /// <param name="plugin">The application plugin.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder Use(IApplicationPlugin plugin);

    /// <summary>Records application-context configuration.</summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder ConfigureApplication(Action<IApplicationContext> configure);

    /// <summary>Builds the configured application.</summary>
    /// <returns>The application.</returns>
    IApplication Build();
}
