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
    private readonly ApplicationOptions _options = new();
    private readonly BrowserApplicationOptions _browserOptions = new();

    /// <summary>Creates a command-buffered browser application builder targeting <c>#app</c>.</summary>
    public BrowserApplicationBuilder()
    {
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

    /// <summary>Configures Browser mount targeting and hydration behavior.</summary>
    /// <param name="configure">The Browser-options action applied before build.</param>
    /// <returns>This builder.</returns>
    public BrowserApplicationBuilder ConfigureBrowser(
        Action<BrowserApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_browserOptions);
        return this;
    }

    /// <summary>Builds the configured browser application.</summary>
    /// <returns>The browser application.</returns>
    public BrowserApplication Build()
    {
        ArgumentException.ThrowIfNullOrEmpty(
            _browserOptions.MountTargetSelector);
        VirtualNode rootComponent = _options.RootComponent
            ?? throw new InvalidOperationException(
                "Configure ApplicationOptions.RootComponent before building the application.");
        IComponentFactory components = _options.Components
            ?? throw new InvalidOperationException(
                "Configure ApplicationOptions.Components with a component resolver.");
        var applicationOptions = new ApplicationOptions
        {
            RootComponent = rootComponent,
            Components = new BrowserComponentFactory(components),
            Services = _options.Services,
            State = _options.State,
            Directives = _options.Directives ?? BrowserDirectiveResolver.Instance,
            ErrorHandler = _options.ErrorHandler,
            WarnHandler = _options.WarnHandler,
            EventObserver = _options.EventObserver,
        };
        ApplicationContext context = new(applicationOptions);
        return BrowserApplication.Create(
            context,
            _browserOptions.Hydrate,
            _browserOptions.MountTargetSelector);
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
