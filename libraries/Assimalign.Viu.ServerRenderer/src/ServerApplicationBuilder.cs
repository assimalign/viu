using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Composes a host-neutral server application from a root component tree and independently supplied
/// component, service, and state resolvers.
/// </summary>
public sealed class ServerApplicationBuilder : ApplicationBuilder
{
    /// <inheritdoc/>
    public override ServerApplicationBuilder AddRootComponent(IComponent component)
    {
        base.AddRootComponent(component);
        return this;
    }

    /// <inheritdoc/>
    public override ServerApplicationBuilder AddComponentFactory(IComponentFactory components)
    {
        base.AddComponentFactory(components);
        return this;
    }

    /// <inheritdoc/>
    public override ServerApplicationBuilder AddServiceProvider(IServiceProvider services)
    {
        base.AddServiceProvider(services);
        return this;
    }

    /// <inheritdoc/>
    public override ServerApplicationBuilder AddStateRegistry(IStateStoreRegistry state)
    {
        base.AddStateRegistry(state);
        return this;
    }

    /// <inheritdoc/>
    public override ServerApplicationBuilder AddDirectiveResolver(IDirectiveResolver directives)
    {
        base.AddDirectiveResolver(directives);
        return this;
    }

    /// <inheritdoc/>
    public override ServerApplicationBuilder ConfigureApplication(
        Action<ApplicationOptions> configure)
    {
        base.ConfigureApplication(configure);
        return this;
    }

    /// <summary>Builds the configured server-render composition object.</summary>
    /// <returns>The configured server-render application.</returns>
    public ServerRenderApplication Build()
    {
        return new ServerRenderApplication(CreateContext());
    }
}
