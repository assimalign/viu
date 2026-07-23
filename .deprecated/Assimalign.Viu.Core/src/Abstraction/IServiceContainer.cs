using System;

namespace Assimalign.Viu;

/// <summary>
/// The bring-your-own dependency-injection registration surface ([V01.01.03.24]) — the Viu counterpart
/// of <c>Microsoft.Extensions.DependencyInjection.IServiceCollection</c> plus its
/// <c>BuildServiceProvider()</c>, carried by our own type so Core takes no dependency on that package.
/// Registrations are collected with <see cref="Add"/> (or the ergonomic
/// <see cref="ServiceContainerExtensions"/> helpers), then <see cref="Build"/> produces the
/// <see cref="IServiceProvider"/> the application resolves services from
/// (<see cref="IApplicationContext.ServicesProvider"/>). Supply a custom implementation to
/// <see cref="IApplicationBuilder.UseServiceContainer"/> to bring any container.
/// <para>
/// <b>Name shadowing (Arc 2 ratified decision 2).</b> This deliberately shadows the legacy
/// <c>System.ComponentModel.Design.IServiceContainer</c> (a designer-era interface effectively absent
/// from modern app code); the shadowing is recorded in the Core <c>DESIGN.md</c>. Not thread-safe
/// (single-threaded JS event-loop model).
/// </para>
/// </summary>
public interface IServiceContainer
{
    /// <summary>
    /// Adds a <paramref name="service"/> registration (upstream:
    /// <c>IServiceCollection.Add(ServiceDescriptor)</c>) and returns this container for chaining. The
    /// default container freezes at <see cref="Build"/>: adding after building throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="service">The service registration to add.</param>
    /// <returns>This container, for chaining.</returns>
    IServiceContainer Add(ServiceRegistration service);

    /// <summary>
    /// Builds the <see cref="IServiceProvider"/> from the recorded registrations (upstream:
    /// <c>BuildServiceProvider()</c>). Called once by <see cref="IApplicationBuilder.Build"/>; the built
    /// application owns and disposes the returned provider.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    IServiceProvider Build();
}
