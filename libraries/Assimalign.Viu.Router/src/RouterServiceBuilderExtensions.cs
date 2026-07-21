using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Router;

/// <summary>
/// Application-builder extensions that register a <see cref="Router"/> through the bring-your-own
/// dependency-injection surface ([V01.01.03.24]) — the service-based counterpart of the app-wide
/// <c>builder.Provide(RouterInjectionKeys.Router, router)</c>. This is the additive .NET-idiomatic path
/// introduced by the reshape; the existing provide-based path keeps working unchanged, and the router
/// components resolve <b>service-first-then-provide</b> (see <see cref="RouterView"/>/
/// <see cref="RouterLink"/>), so an app configured either way resolves the same router.
/// </summary>
public static class RouterServiceBuilderExtensions
{
    /// <summary>
    /// Registers <paramref name="router"/> on the builder both as an application service singleton
    /// (<c>builder.Services.AddSingleton(router)</c>) <b>and</b> as an app-wide provide under
    /// <see cref="RouterInjectionKeys.Router"/> — so <see cref="RouterView"/>/<see cref="RouterLink"/>/
    /// <see cref="RouterGuards"/> resolve it through either the service provider or the provide chain.
    /// Keeping both preserves parity with apps that still provide the router directly. Returns the
    /// builder for chaining.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="router">The router to install.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="router"/> is null.</exception>
    public static IApplicationBuilder AddRouter(this IApplicationBuilder builder, Router router)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(router);
        // Service path: the router as an app service singleton.
        builder.Services.AddSingleton(router);
        // Provide-path parity: app-wide provide (upstream: app.provide(routerKey, router)) so the
        // existing inject-based resolution keeps working.
        builder.Provide(RouterInjectionKeys.Router, router);
        return builder;
    }
}
