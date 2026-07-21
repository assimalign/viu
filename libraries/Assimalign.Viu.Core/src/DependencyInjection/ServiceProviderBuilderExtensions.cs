using System;

namespace Assimalign.Viu;

/// <summary>
/// Ergonomic registration helpers over <see cref="IServiceProviderBuilder.Add"/> — the Viu
/// counterparts of <c>ServiceCollectionServiceExtensions.AddSingleton/AddScoped/AddTransient</c>
/// (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.servicecollectionserviceextensions).
/// Each builds a <see cref="ServiceRegistration"/> from a factory delegate and adds it, so registration
/// stays AOT-safe (no reflection activation). Generic helpers target reference types; register a
/// value-typed or differently-shaped service through <see cref="IServiceProviderBuilder.Add"/> directly.
/// </summary>
public static class ServiceProviderBuilderExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TService"/> as a <see cref="ServiceLifetime.Singleton"/> created
    /// by <paramref name="factory"/> — one instance per application, cached and disposed with the app.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="builder">The service builder.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceProviderBuilder AddSingleton<TService>(this IServiceProviderBuilder builder, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Singleton, provider => factory(provider)));
    }

    /// <summary>
    /// Registers an already-constructed <paramref name="instance"/> as a
    /// <see cref="ServiceLifetime.Singleton"/> for <typeparamref name="TService"/> — the common
    /// app-level pattern for a pre-built router, store registry, or data client. The application
    /// disposes <paramref name="instance"/> if it is <see cref="IDisposable"/>.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="builder">The service builder.</param>
    /// <param name="instance">The singleton instance.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="instance"/> is null.</exception>
    public static IServiceProviderBuilder AddSingleton<TService>(this IServiceProviderBuilder builder, TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        return builder.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Singleton, _ => instance));
    }

    /// <summary>
    /// Registers <typeparamref name="TService"/> as <see cref="ServiceLifetime.Scoped"/> created by
    /// <paramref name="factory"/>. In Viu's default provider the application is the only scope, so this
    /// resolves once per application (isolated across applications); a container adapter with child
    /// scopes gives it full per-scope meaning.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="builder">The service builder.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceProviderBuilder AddScoped<TService>(this IServiceProviderBuilder builder, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Scoped, provider => factory(provider)));
    }

    /// <summary>
    /// Registers <typeparamref name="TService"/> as <see cref="ServiceLifetime.Transient"/> created by
    /// <paramref name="factory"/> — a fresh instance on every resolution. The default provider does not
    /// dispose transient instances (a disposable transient is the caller's responsibility).
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="builder">The service builder.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceProviderBuilder AddTransient<TService>(this IServiceProviderBuilder builder, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Transient, provider => factory(provider)));
    }
}
