using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Store;

/// <summary>
/// Application-builder extensions that register a <see cref="StoreRegistry"/> through the
/// bring-your-own dependency-injection surface ([V01.01.03.24]) — the service-based counterpart of
/// <c>App.Use(registry.AsPlugin())</c>. This is the additive .NET-idiomatic path introduced by the
/// reshape; the existing plugin/provide path keeps working unchanged, and store resolution
/// (<see cref="StoreDefinition{TStore}.UseStore()"/>) tries services first, then the provide chain,
/// then the ambient registry, so an app configured either way resolves the same registry.
/// </summary>
public static class StoreServiceBuilderExtensions
{
    /// <summary>
    /// Registers <paramref name="registry"/> on the builder both as an application service singleton
    /// (<c>builder.Services.AddSingleton(registry)</c>) <b>and</b> through the store plugin
    /// (<c>builder.Use(registry.AsPlugin())</c>) — so component resolution finds it through the service
    /// provider or the provide chain, and the ambient <see cref="Stores.ActiveRegistry"/> is set for
    /// non-component code, exactly as before. Because the registry is now an owned service singleton,
    /// disposing the built application disposes it (cascading to every store's scope). Returns the
    /// builder for chaining.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="registry">The per-app store registry to install.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="registry"/> is null.</exception>
    public static IApplicationBuilder AddStore(this IApplicationBuilder builder, StoreRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registry);
        // Service path: the registry as an owned singleton (disposed with the app).
        builder.Services.AddSingleton(registry);
        // Provide-path parity: app-wide provide of the registry + set the ambient active registry
        // (upstream: app.use(pinia)). Keeping both means provide-based resolution stays intact.
        builder.Use(registry.AsPlugin());
        return builder;
    }
}
