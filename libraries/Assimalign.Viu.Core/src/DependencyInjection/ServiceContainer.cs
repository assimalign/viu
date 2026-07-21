using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The default <see cref="IServiceContainer"/> — collects <see cref="ServiceRegistration"/>s and
/// <see cref="Build"/>s an AOT-safe, factory-delegate <see cref="IServiceProvider"/> (the Viu
/// counterpart of <c>ServiceCollection</c> + <c>ServiceCollection.BuildServiceProvider()</c>,
/// https://learn.microsoft.com/dotnet/core/extensions/dependency-injection, carried by our own type so
/// Core takes no dependency on that package). It is the container an <see cref="IApplicationBuilder"/>
/// uses unless the app supplies its own with <see cref="IApplicationBuilder.UseServiceContainer"/>.
/// <para>
/// The built provider has no reflection activation or constructor discovery: every service comes from
/// its registration factory, so it is trimming- and WASM/NativeAOT-safe. It supports
/// <see cref="ServiceLifetime.Singleton"/>/<see cref="ServiceLifetime.Scoped"/> (cached for the
/// application's lifetime) and <see cref="ServiceLifetime.Transient"/> (created per resolution); it
/// deliberately does <b>not</b> implement child scopes, open generics, decorators, multi-registration
/// <c>IEnumerable&lt;T&gt;</c> resolution, or keyed services — bring a container adapter through
/// <see cref="IServiceContainer"/> for those. Register services with the
/// <see cref="ServiceContainerExtensions"/> helpers. Not thread-safe (single-threaded JS
/// event-loop model).
/// </para>
/// <para>
/// <b>Freeze semantics ([V01.01.03.27]).</b> <see cref="IApplicationBuilder.Build"/> calls
/// <see cref="Build"/> exactly once; the container freezes then, so a later <see cref="Add"/> throws
/// <see cref="InvalidOperationException"/> with an actionable message (registrations must be complete
/// before the application is built).
/// </para>
/// </summary>
public sealed class ServiceContainer : IServiceContainer
{
    private readonly List<ServiceRegistration> _registrations = [];
    private bool _built;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The container was already <see cref="Build"/>t (frozen).</exception>
    public IServiceContainer Add(ServiceRegistration registration)
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "Cannot add a service registration after the container has been built. Register all services "
                + "(builder.Services / ConfigureServices) before calling builder.Build().");
        }
        _registrations.Add(registration);
        return this;
    }

    /// <summary>The registrations recorded so far, in registration order (last wins per service type at build).</summary>
    public IReadOnlyList<ServiceRegistration> Registrations => _registrations;

    /// <summary>
    /// Builds the default AOT-safe provider from the recorded registrations and freezes the container
    /// (a later <see cref="Add"/> throws). Each call returns a fresh, independent provider from the
    /// frozen registrations. The application that receives the provider owns it and disposes it.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    public IServiceProvider Build()
    {
        _built = true;
        return new FactoryServiceProvider(_registrations);
    }
}
