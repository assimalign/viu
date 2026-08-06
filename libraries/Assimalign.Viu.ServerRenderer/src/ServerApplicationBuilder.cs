using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Composes a host-neutral server-render application through options that are frozen at build time.
/// </summary>
/// <remarks>
/// This builder creates a per-render composition object, not a persistent <see cref="IApplication"/>
/// host. It borrows every configured dependency and never disposes one. Specified by
/// <c>[APP-2]</c>, <c>[APP-6]</c>, and <c>[SSR-2]</c>.
/// </remarks>
public sealed class ServerApplicationBuilder
{
    private readonly ApplicationOptions _options = new();

    /// <summary>Configures the composition and diagnostics to snapshot when the application is built.</summary>
    /// <param name="configure">The options action to apply immediately.</param>
    /// <returns>This builder for further configuration.</returns>
    public ServerApplicationBuilder ConfigureApplication(
        Action<ApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    /// <summary>Builds a server-render composition from the current option values.</summary>
    /// <returns>
    /// A server-render application whose context retains an immutable snapshot of the configured
    /// composition and diagnostics.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A required root component, component factory, or service provider was not configured.
    /// </exception>
    public ServerRenderApplication Build()
    {
        IComponent rootComponent = _options.RootComponent
            ?? throw new InvalidOperationException(
                "Configure a root component before building the application.");
        IComponentFactory components = _options.Components
            ?? throw new InvalidOperationException(
                "Configure a component factory before building the application.");
        IServiceProvider services = _options.Services
            ?? throw new InvalidOperationException(
                "Configure a service provider before building the application.");

        return new ServerRenderApplication(
            new ApplicationContext(
                rootComponent,
                components,
                services,
                _options.State,
                _options.Directives,
                _options));
    }
}
