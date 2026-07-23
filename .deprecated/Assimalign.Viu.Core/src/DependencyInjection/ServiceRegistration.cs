using System;

namespace Assimalign.Viu;

/// <summary>
/// A single service registration handed to an <see cref="IServiceContainer"/> — the Viu
/// counterpart of <c>Microsoft.Extensions.DependencyInjection.ServiceDescriptor</c>
/// (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.servicedescriptor),
/// carried by our own type so Core takes no dependency on that package. It couples a service
/// <see cref="ServiceType"/>, its <see cref="Lifetime"/>, and a <see cref="Factory"/> that creates the
/// instance from the provider.
/// <para>
/// <b>Factory-only, AOT-safe by construction.</b> Every service is produced by an explicit
/// <see cref="Factory"/> delegate — there is no constructor discovery, assembly scanning, or reflective
/// activation, so registrations are trimming- and WASM/NativeAOT-safe. The ergonomic
/// <see cref="ServiceContainerExtensions"/> (<c>AddSingleton</c>/<c>AddScoped</c>/<c>AddTransient</c>)
/// build these descriptors for you.
/// </para>
/// </summary>
public readonly struct ServiceRegistration
{
    /// <summary>
    /// Creates a registration for <paramref name="serviceType"/> with the given
    /// <paramref name="lifetime"/> and <paramref name="factory"/>.
    /// </summary>
    /// <param name="serviceType">The service type consumers resolve by.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <param name="factory">Creates the instance from the resolving <see cref="IServiceProvider"/>; the returned object must be assignable to <paramref name="serviceType"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> or <paramref name="factory"/> is null.</exception>
    public ServiceRegistration(Type serviceType, ServiceLifetime lifetime, Func<IServiceProvider, object> factory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(factory);
        ServiceType = serviceType;
        Lifetime = lifetime;
        Factory = factory;
    }

    /// <summary>The service type consumers resolve by (upstream: <c>ServiceDescriptor.ServiceType</c>).</summary>
    public Type ServiceType { get; }

    /// <summary>The service lifetime (upstream: <c>ServiceDescriptor.Lifetime</c>).</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// The factory that creates the instance from the resolving provider (upstream:
    /// <c>ServiceDescriptor.ImplementationFactory</c>). Never null on a registration created through
    /// the constructor.
    /// </summary>
    public Func<IServiceProvider, object> Factory { get; }
}
