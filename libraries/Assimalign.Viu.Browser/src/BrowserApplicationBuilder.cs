using System;
using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Composes a browser application from a root component tree, component factory, service provider,
/// optional state registry, and frozen diagnostic options.
/// </summary>
/// <remarks>
/// The builder inherits the host-neutral configuration surface from
/// <see cref="ApplicationBuilder"/>. It does not construct a dependency-injection container and
/// the resulting application borrows every supplied resolver.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationBuilder : ApplicationBuilder
{
    private readonly bool _useCommandBuffer;
    private readonly bool _hydrate;
    private readonly string _mountTargetSelector;

    internal BrowserApplicationBuilder(
        bool useCommandBuffer,
        bool hydrate,
        string mountTargetSelector = BrowserApplication.DefaultMountTargetSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);
        _useCommandBuffer = useCommandBuffer;
        _hydrate = hydrate;
        _mountTargetSelector = mountTargetSelector;
        AddDirectiveResolver(BrowserDirectiveResolver.Instance);
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder AddRootComponent(IComponent component)
    {
        base.AddRootComponent(component);
        return this;
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder AddComponentFactory(IComponentFactory components)
    {
        base.AddComponentFactory(components);
        return this;
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder AddServiceProvider(IServiceProvider services)
    {
        base.AddServiceProvider(services);
        return this;
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder AddStateRegistry(IStateStoreRegistry state)
    {
        base.AddStateRegistry(state);
        return this;
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder AddDirectiveResolver(IDirectiveResolver directives)
    {
        base.AddDirectiveResolver(directives);
        return this;
    }

    /// <inheritdoc/>
    public override BrowserApplicationBuilder ConfigureApplication(
        Action<ApplicationOptions> configure)
    {
        base.ConfigureApplication(configure);
        return this;
    }

    /// <summary>
    /// Builds the configured browser application.
    /// </summary>
    /// <returns>The browser application.</returns>
    public BrowserApplication Build()
    {
        IApplicationContext context = CreateContext(
            static components => new BrowserComponentFactory(components));
        return BrowserApplication.Create(
            context,
            _useCommandBuffer,
            _hydrate,
            _mountTargetSelector);
    }
}
