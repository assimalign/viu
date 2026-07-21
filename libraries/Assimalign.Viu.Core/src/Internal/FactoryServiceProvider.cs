using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The default <see cref="IServiceProvider"/> produced by <see cref="ServiceProviderBuilder.Build"/> —
/// an AOT-safe, factory-delegate registry. It has no reflection activation, no constructor discovery,
/// and no dependency on <c>Microsoft.Extensions.DependencyInjection</c>: every service is created by a
/// user-supplied <see cref="ServiceRegistration.Factory"/>, so it is trimming- and WASM/NativeAOT-safe.
/// <para>
/// <b>Lifetimes.</b> The application is the single root scope (this provider creates no child scopes).
/// <see cref="ServiceLifetime.Singleton"/> and <see cref="ServiceLifetime.Scoped"/> are both created on
/// first resolution and cached for the provider's lifetime; <see cref="ServiceLifetime.Transient"/>
/// runs its factory on every resolution. Last registration wins for a given service type (upstream
/// parity with <c>ServiceCollection</c>). A request for <see cref="IServiceProvider"/> returns this
/// provider itself. An unregistered service returns <c>null</c> (the <see cref="IServiceProvider"/>
/// contract).
/// </para>
/// <para>
/// <b>Disposal.</b> Cached (singleton/scoped) instances that are <see cref="IDisposable"/> are tracked
/// and disposed in reverse creation order when this provider disposes (upstream parity). Transient
/// disposables are <i>not</i> tracked — a disposable transient is the caller's responsibility (a
/// deliberate, documented divergence from the built-in container that keeps the provider leak-free).
/// A re-entrant self-resolution (a factory that resolves its own service type) throws
/// <see cref="InvalidOperationException"/> instead of overflowing the stack. Internal — the public
/// surface is <see cref="IServiceProvider"/>. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
internal sealed class FactoryServiceProvider : IServiceProvider, IDisposable
{
    private readonly Dictionary<Type, ServiceRegistration> _registrations;
    // Cached singleton/scoped instances (the app is the root scope, so both cache here), keyed by
    // service type. Null-factory results are cached too (a factory may legitimately return null? no —
    // factories return object; a miss is "not in _registrations"). Keyed by ServiceType.
    private readonly Dictionary<Type, object> _cache = new();
    // Owned disposables (cached singleton/scoped instances that implement IDisposable), disposed in
    // reverse creation order — the last created is the first disposed (upstream parity).
    private readonly List<IDisposable> _disposables = [];
    // Guards against a factory resolving its own service type (a dependency cycle) — without this a
    // cycle recurses until the stack overflows. Small and single-threaded, so a HashSet is enough.
    private readonly HashSet<Type> _resolving = [];
    private bool _disposed;

    internal FactoryServiceProvider(IReadOnlyList<ServiceRegistration> registrations)
    {
        _registrations = new Dictionary<Type, ServiceRegistration>(registrations.Count);
        // Last registration wins for a service type (upstream: the last ServiceDescriptor added is the
        // one GetService resolves).
        for (var i = 0; i < registrations.Count; i++)
        {
            _registrations[registrations[i].ServiceType] = registrations[i];
        }
    }

    /// <summary>
    /// Resolves the service registered under <paramref name="serviceType"/> (upstream:
    /// <c>IServiceProvider.GetService</c>). Returns this provider for
    /// <see cref="IServiceProvider"/>, the cached instance for a singleton/scoped registration, a fresh
    /// instance for a transient registration, or <c>null</c> when nothing is registered.
    /// </summary>
    /// <param name="serviceType">The service type to resolve.</param>
    /// <returns>The resolved service, or null when unregistered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="InvalidOperationException">A factory resolves its own service type (a cycle).</exception>
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ObjectDisposedException.ThrowIf(_disposed, this);
        // The provider resolves itself (upstream parity: GetService(typeof(IServiceProvider)) == the
        // provider), so a factory can capture the provider and resolve dependencies.
        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }
        if (!_registrations.TryGetValue(serviceType, out var registration))
        {
            return null;
        }
        if (registration.Lifetime == ServiceLifetime.Transient)
        {
            return Create(registration);
        }
        // Singleton/scoped: one cached instance for the provider's (the app's) lifetime.
        if (_cache.TryGetValue(serviceType, out var cached))
        {
            return cached;
        }
        var instance = Create(registration);
        _cache[serviceType] = instance;
        if (instance is IDisposable disposable)
        {
            _disposables.Add(disposable);
        }
        return instance;
    }

    private object Create(ServiceRegistration registration)
    {
        if (!_resolving.Add(registration.ServiceType))
        {
            throw new InvalidOperationException(
                $"A dependency cycle was detected while resolving service \"{registration.ServiceType}\": "
                + "its factory resolves its own service type. Break the cycle in the registration factories.");
        }
        try
        {
            return registration.Factory(this)
                ?? throw new InvalidOperationException(
                    $"The factory for service \"{registration.ServiceType}\" returned null.");
        }
        finally
        {
            _resolving.Remove(registration.ServiceType);
        }
    }

    /// <summary>
    /// Disposes every owned singleton/scoped instance that implements <see cref="IDisposable"/>, in
    /// reverse creation order, and clears the caches. Idempotent — a second call is a no-op. Transient
    /// instances are not owned and are not disposed here.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        // Reverse creation order: a later-created service may depend on an earlier one, so it is torn
        // down first (upstream parity with the built-in container).
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
        _disposables.Clear();
        _cache.Clear();
    }
}
