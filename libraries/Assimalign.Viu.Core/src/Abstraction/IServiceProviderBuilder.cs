using System;

namespace Assimalign.Viu;

/// <summary>
/// The bring-your-own dependency-injection bridge — the small interface a Viu application builder
/// registers services on and, at build time, turns into a <see cref="System.IServiceProvider"/> that
/// hangs off the application (<see cref="IApplication.Services"/>). Implement it over any container
/// (the built-in <see cref="ServiceProviderBuilder"/>, or an adapter over
/// <c>Microsoft.Extensions.DependencyInjection</c>, Autofac, …): the interface is deliberately minimal
/// — <see cref="Add"/> takes registrations, <see cref="Build"/> produces the provider — so an adapter
/// implements exactly two members. It is the .NET-idiomatic counterpart of
/// <c>IServiceProviderFactory&lt;TContainerBuilder&gt;</c>
/// (https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iserviceproviderfactory-1),
/// carried by our own type so Core takes no dependency on that package.
/// <para>
/// This has no Vue upstream: it is app-level dependency injection, layered <b>beside</b> — never
/// replacing — Vue's component-tree <c>Provide</c>/<c>Inject</c> with <see cref="InjectionKey{T}"/>
/// (see <see cref="DependencyInjection"/>), which stays the Vue-semantic feature. App-level singleton
/// wiring (a Pinia-style store registry, a router, a data client) is what moves to services.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IServiceProviderBuilder
{
    /// <summary>
    /// Records <paramref name="registration"/> (upstream: adding a <c>ServiceDescriptor</c> to an
    /// <c>IServiceCollection</c>). Returns the builder for chaining. The ergonomic
    /// <see cref="ServiceProviderBuilderExtensions"/> methods (<c>AddSingleton</c> and friends) call
    /// this. A container adapter maps <paramref name="registration"/> onto its own registration API.
    /// </summary>
    /// <param name="registration">The service to register.</param>
    /// <returns>This builder, for chaining.</returns>
    IServiceProviderBuilder Add(ServiceRegistration registration);

    /// <summary>
    /// Builds the <see cref="System.IServiceProvider"/> from the recorded registrations (upstream:
    /// <c>IServiceCollection.BuildServiceProvider()</c> / the container factory's
    /// <c>CreateServiceProvider</c>). The application builder calls this once and attaches the result
    /// to the application verbatim, so the returned provider is exactly what
    /// <see cref="IApplication.Services"/> exposes. The application owns the returned provider and
    /// disposes it (if <see cref="System.IDisposable"/>) when the application disposes.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    IServiceProvider Build();
}
