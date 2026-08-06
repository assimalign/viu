using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Carries the immutable application composition used to render one component tree on the server.
/// </summary>
/// <remarks>
/// This object is not a persistent runnable host and does not implement <see cref="IApplication"/>.
/// Each render receives its own cancellation token and bypasses top-level application lifetime
/// middleware. The component factory, service provider, and state registry remain borrowed from
/// their composition root and are never disposed by ServerRenderer. Specified by <c>[SSR-2]</c>,
/// <c>[APP-6]</c>, and <c>[APP-7]</c>.
/// </remarks>
public sealed class ServerRenderApplication
{
    /// <summary>Creates a server-render composition over an independently composed context.</summary>
    /// <param name="context">The immutable application context used by each render.</param>
    public ServerRenderApplication(IApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Creates a server-render composition from independently supplied services.</summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied service resolver.</param>
    /// <param name="state">The optional application state registry.</param>
    public ServerRenderApplication(
        IComponent rootComponent,
        IComponentFactory components,
        IServiceProvider services,
        IStateStoreRegistry? state = null)
        : this(new ApplicationContext(rootComponent, components, services, state))
    {
    }

    /// <summary>Creates an empty server-render application builder.</summary>
    /// <returns>The new builder.</returns>
    public static ServerApplicationBuilder CreateBuilder() => new();

    /// <summary>Creates a builder initialized with the supplied application composition.</summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied service resolver.</param>
    /// <returns>The initialized builder.</returns>
    /// <remarks>
    /// The supplied values are applied through <see cref="ApplicationOptions"/> so subsequent
    /// configuration and <see cref="ServerApplicationBuilder.Build"/> use the same composition
    /// surface.
    /// </remarks>
    public static ServerApplicationBuilder CreateBuilder(
        IComponent rootComponent,
        IComponentFactory components,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(services);

        return new ServerApplicationBuilder()
            .ConfigureApplication(options =>
            {
                options.RootComponent = rootComponent;
                options.Components = components;
                options.Services = services;
            });
    }

    /// <summary>
    /// Gets the immutable root component, resolver, diagnostics, and optional state composition used
    /// by each render.
    /// </summary>
    public IApplicationContext Context { get; }
}
