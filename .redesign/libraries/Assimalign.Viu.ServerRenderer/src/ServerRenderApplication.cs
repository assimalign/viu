using System;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Carries the immutable application composition used by one server-render operation.
/// </summary>
/// <remarks>
/// This value is not a persistent runnable host and does not implement <see cref="IApplication"/>.
/// It owns no mounted lifetime, bypasses application middleware, and never disposes its borrowed
/// component factory, services, directives, or state. Specified by <c>[SSR-2]</c>,
/// <c>[SSR-8]</c>, and <c>[SSR-9]</c>.
/// </remarks>
public sealed class ServerRenderApplication
{
    /// <summary>Initializes a server-render composition over an existing application context.</summary>
    /// <param name="context">The immutable, host-neutral application context.</param>
    /// <remarks>Ownership remains with the caller as required by <c>[SSR-9]</c>.</remarks>
    public ServerRenderApplication(IApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Initializes a server-render composition from independently supplied services.</summary>
    /// <param name="rootComponent">The root value in Viu's virtual-node algebra.</param>
    /// <param name="components">The borrowed component registration resolver.</param>
    /// <param name="services">The optional borrowed application service provider.</param>
    /// <remarks>Specified by <c>[SSR-2]</c> and <c>[SSR-9]</c>.</remarks>
    public ServerRenderApplication(
        VirtualNode rootComponent,
        IComponentFactory components,
        IServiceProvider? services = null)
        : this(CreateContext(rootComponent, components, services))
    {
    }

    /// <summary>Creates an empty server-render application builder.</summary>
    /// <returns>A builder whose values freeze when <see cref="ServerApplicationBuilder.Build"/> runs.</returns>
    /// <remarks>Specified by <c>[APP-2]</c> and <c>[SSR-2]</c>.</remarks>
    public static ServerApplicationBuilder CreateBuilder() => new();

    /// <summary>Creates a builder initialized with the supplied application composition.</summary>
    /// <param name="rootComponent">The root value in Viu's virtual-node algebra.</param>
    /// <param name="components">The borrowed component registration resolver.</param>
    /// <param name="services">The optional borrowed application service provider.</param>
    /// <returns>The initialized builder.</returns>
    /// <remarks>Specified by <c>[APP-2]</c>, <c>[APP-6]</c>, and <c>[SSR-2]</c>.</remarks>
    public static ServerApplicationBuilder CreateBuilder(
        VirtualNode rootComponent,
        IComponentFactory components,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);

        return new ServerApplicationBuilder()
            .ConfigureApplication(options =>
            {
                options.RootComponent = rootComponent;
                options.Components = components;
                options.Services = services;
            });
    }

    /// <summary>
    /// Gets the immutable root, component resolver, diagnostics, and optional service/state
    /// composition used by every render of this application.
    /// </summary>
    /// <remarks>The contained dependencies remain borrowed under <c>[APP-6]</c> and <c>[SSR-9]</c>.</remarks>
    public IApplicationContext Context { get; }

    private static ApplicationContext CreateContext(
        VirtualNode rootComponent,
        IComponentFactory components,
        IServiceProvider? services)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);
        ApplicationOptions options = new()
        {
            RootComponent = rootComponent,
            Components = components,
            Services = services,
        };
        return new ApplicationContext(options);
    }
}
