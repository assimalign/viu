using System.Runtime.Versioning;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Composes a browser application from a root component tree, component factory, service provider,
/// optional state registry, and plugins.
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

    internal BrowserApplicationBuilder(bool useCommandBuffer, bool hydrate)
    {
        _useCommandBuffer = useCommandBuffer;
        _hydrate = hydrate;
        UseDirectiveResolver(BrowserDirectiveResolver.Instance);
    }

    /// <summary>
    /// Builds the configured browser application.
    /// </summary>
    /// <returns>The browser application.</returns>
    public override BrowserApplication Build()
    {
        IApplicationContext configuredContext = CreateContext();
        IApplicationContext context = new ApplicationContext(
            configuredContext.RootComponent,
            new BrowserComponentFactory(configuredContext.Components),
            configuredContext.Services,
            configuredContext.State,
            configuredContext.Directives);
        BrowserApplication application =
            BrowserApplication.Create(context, _useCommandBuffer, _hydrate);
        ApplyConfiguration(application);
        return application;
    }
}
