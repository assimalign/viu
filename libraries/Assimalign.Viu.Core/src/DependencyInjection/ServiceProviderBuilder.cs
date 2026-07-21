using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The default <see cref="IServiceProviderBuilder"/> — collects <see cref="ServiceRegistration"/>s and
/// <see cref="Build"/>s an AOT-safe, factory-delegate <see cref="IServiceProvider"/> (the Viu
/// counterpart of <c>ServiceCollection</c> +
/// <c>ServiceCollection.BuildServiceProvider()</c>,
/// https://learn.microsoft.com/dotnet/core/extensions/dependency-injection, carried by our own type so
/// Core takes no dependency on that package). It is the builder an
/// <see cref="IApplicationBuilder"/> uses unless the app supplies its own with
/// <see cref="IApplicationBuilder.UseServiceProviderBuilder"/>.
/// <para>
/// The built provider has no reflection activation or constructor discovery: every service comes from
/// its registration factory, so it is trimming- and WASM/NativeAOT-safe. It supports
/// <see cref="ServiceLifetime.Singleton"/>/<see cref="ServiceLifetime.Scoped"/> (cached for the
/// application's lifetime) and <see cref="ServiceLifetime.Transient"/> (created per resolution); it
/// deliberately does <b>not</b> implement child scopes, open generics, decorators, multi-registration
/// <c>IEnumerable&lt;T&gt;</c> resolution, or keyed services — bring a container adapter through
/// <see cref="IServiceProviderBuilder"/> for those. Register services with the
/// <see cref="ServiceProviderBuilderExtensions"/> helpers. Not thread-safe (single-threaded JS
/// event-loop model).
/// </para>
/// </summary>
public sealed class ServiceProviderBuilder : IServiceProviderBuilder
{
    private readonly List<ServiceRegistration> _registrations = [];

    /// <inheritdoc/>
    public IServiceProviderBuilder Add(ServiceRegistration registration)
    {
        _registrations.Add(registration);
        return this;
    }

    /// <summary>The registrations recorded so far, in registration order (last wins per service type at build).</summary>
    public IReadOnlyList<ServiceRegistration> Registrations => _registrations;

    /// <summary>
    /// Builds the default AOT-safe provider from the recorded registrations. Each call returns a fresh,
    /// independent provider (so two applications built from the same builder are isolated). The
    /// application that receives the provider owns it and disposes it.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    public IServiceProvider Build() => new FactoryServiceProvider(_registrations);
}
