using System;
using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Composes a browser application by snapshotting one <see cref="ApplicationOptions"/> instance.
/// </summary>
/// <remarks>
/// The builder does not construct a dependency-injection container. The resulting application
/// borrows every supplied resolver. Specified by <c>[APP-2]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationBuilder : IApplicationBuilder
{
    private readonly ApplicationOptions _options = new()
    {
        Directives = BrowserDirectiveResolver.Instance,
    };
    private readonly bool _useCommandBuffer;
    private readonly bool _hydrate;
    private readonly string _mountTargetSelector;

    /// <summary>Creates a browser application builder targeting <c>#app</c>.</summary>
    /// <param name="useCommandBuffer">
    /// Whether host mutations should be serialized into one command frame per explicit render
    /// boundary.
    /// </param>
    public BrowserApplicationBuilder(bool useCommandBuffer = false)
        : this(
            useCommandBuffer,
            hydrate: false,
            BrowserApplication.DefaultMountTargetSelector)
    {
    }

    internal BrowserApplicationBuilder(
        bool useCommandBuffer,
        bool hydrate,
        string mountTargetSelector = BrowserApplication.DefaultMountTargetSelector)
    {
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);
        _useCommandBuffer = useCommandBuffer;
        _hydrate = hydrate;
        _mountTargetSelector = mountTargetSelector;
    }

    /// <summary>Configures composition and diagnostics that are frozen by each build.</summary>
    /// <param name="configure">The application-options action.</param>
    /// <returns>This builder.</returns>
    public BrowserApplicationBuilder ConfigureApplication(
        Action<ApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    /// <summary>Builds the configured browser application.</summary>
    /// <returns>The browser application.</returns>
    public BrowserApplication Build()
    {
        IComponent rootComponent = _options.RootComponent
            ?? throw new InvalidOperationException(
                "Configure ApplicationOptions.RootComponent before building the application.");
        IComponentFactory components = _options.Components
            ?? throw new InvalidOperationException(
                "ApplicationOptions.Components cannot be null.");
        IServiceProvider services = _options.Services
            ?? throw new InvalidOperationException(
                "ApplicationOptions.Services cannot be null.");
        ApplicationContext context = new(
            rootComponent,
            new BrowserComponentFactory(components),
            services,
            _options.State,
            _options.Directives,
            _options);
        return BrowserApplication.Create(
            context,
            _useCommandBuffer,
            _hydrate,
            _mountTargetSelector);
    }

    IApplicationBuilder IApplicationBuilder.ConfigureApplication(
        Action<ApplicationOptions> configure)
    {
        return ConfigureApplication(configure);
    }

    IApplication IApplicationBuilder.Build()
    {
        return Build();
    }
}
