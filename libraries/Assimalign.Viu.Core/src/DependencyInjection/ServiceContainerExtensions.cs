using System;

namespace Assimalign.Viu;

/// <summary>
/// Ergonomic registration helpers over <see cref="IServiceContainer.Add"/> — the Viu
/// counterparts of <c>ServiceCollectionServiceExtensions.AddSingleton/AddScoped/AddTransient</c>
/// (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.servicecollectionserviceextensions).
/// Each builds a <see cref="ServiceRegistration"/> from a factory delegate and adds it, so registration
/// stays AOT-safe (no reflection activation). Generic helpers target reference types; register a
/// value-typed or differently-shaped service through <see cref="IServiceContainer.Add"/> directly. Every
/// helper returns the container so registrations chain.
/// </summary>
public static class ServiceContainerExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TService"/> as a <see cref="ServiceLifetime.Singleton"/> created
    /// by <paramref name="factory"/> — one instance per application, cached and disposed with the app.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="services">The service container.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The container, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceContainer AddSingleton<TService>(this IServiceContainer services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        return services.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Singleton, provider => factory(provider)));
    }

    /// <summary>
    /// Registers an already-constructed <paramref name="instance"/> as a
    /// <see cref="ServiceLifetime.Singleton"/> for <typeparamref name="TService"/> — the common
    /// app-level pattern for a pre-built router, store registry, or data client. The application
    /// disposes <paramref name="instance"/> if it is <see cref="IDisposable"/>.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="services">The service container.</param>
    /// <param name="instance">The singleton instance.</param>
    /// <returns>The container, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="instance"/> is null.</exception>
    public static IServiceContainer AddSingleton<TService>(this IServiceContainer services, TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(instance);
        return services.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Singleton, _ => instance));
    }

    /// <summary>
    /// Registers <typeparamref name="TService"/> as <see cref="ServiceLifetime.Scoped"/> created by
    /// <paramref name="factory"/>. In Viu's default provider the application is the only scope, so this
    /// resolves once per application (isolated across applications); a container adapter with child
    /// scopes gives it full per-scope meaning.
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="services">The service container.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The container, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceContainer AddScoped<TService>(this IServiceContainer services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        return services.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Scoped, provider => factory(provider)));
    }

    /// <summary>
    /// Registers <typeparamref name="TService"/> as <see cref="ServiceLifetime.Transient"/> created by
    /// <paramref name="factory"/> — a fresh instance on every resolution. The default provider does not
    /// dispose transient instances (a disposable transient is the caller's responsibility).
    /// </summary>
    /// <typeparam name="TService">The service type consumers resolve by.</typeparam>
    /// <param name="services">The service container.</param>
    /// <param name="factory">Creates the instance from the resolving provider.</param>
    /// <returns>The container, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="factory"/> is null.</exception>
    public static IServiceContainer AddTransient<TService>(this IServiceContainer services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        return services.Add(new ServiceRegistration(typeof(TService), ServiceLifetime.Transient, provider => factory(provider)));
    }
}
