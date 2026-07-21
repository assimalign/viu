using System;

namespace Assimalign.Viu;

/// <summary>
/// Typed resolution helpers over <see cref="IServiceProvider.GetService"/> — the Viu counterparts of
/// <c>ServiceProviderServiceExtensions.GetService&lt;T&gt;/GetRequiredService&lt;T&gt;</c>
/// (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.serviceproviderserviceextensions),
/// carried by our own type so Core takes no dependency on that package. Use them on any provider you
/// hold — an <see cref="IApplication.Services"/>, a <see cref="ComponentInstance.Services"/>, or a
/// bring-your-own provider. Inside component <c>Setup</c>, prefer the
/// <see cref="DependencyInjection.GetService{T}()"/> composition functions, which resolve from the
/// current component's application provider for you.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Resolves <typeparamref name="TService"/> from <paramref name="provider"/>, or returns
    /// <c>null</c> when it is not registered (upstream: <c>GetService&lt;T&gt;</c>).
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The resolved service, or null when unregistered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public static TService? GetService<TService>(this IServiceProvider provider)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetService(typeof(TService)) as TService;
    }

    /// <summary>
    /// Resolves <typeparamref name="TService"/> from <paramref name="provider"/>, throwing
    /// <see cref="InvalidOperationException"/> when it is not registered (upstream:
    /// <c>GetRequiredService&lt;T&gt;</c>).
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The resolved service.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TService"/> is not registered.</exception>
    public static TService GetRequiredService<TService>(this IServiceProvider provider)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException($"No service for type \"{typeof(TService)}\" has been registered.");
    }
}
