using System;

namespace Assimalign.Viu;

/// <summary>
/// Configures and builds an application — the .NET-idiomatic bootstrap seam, shaped after
/// <c>Microsoft.AspNetCore.Builder.WebApplicationBuilder</c> (a builder collects configuration, then
/// <see cref="Build"/> produces the app). It is the Viu counterpart of composing an app before
/// <c>createApp(root)...mount()</c> in Vue (https://vuejs.org/api/application.html): the root
/// component and props are fixed at builder creation, and <see cref="Use"/>/<see cref="Provide{T}"/>/
/// <see cref="Component"/>/<see cref="Directive"/> record configuration that <see cref="Build"/>
/// replays onto the freshly built application in call order.
/// <para>
/// Platform packages supply the entry points and concrete builders: the browser's
/// <c>BrowserApplication.CreateBuilder(root)</c> builds a browser app, the server renderer's
/// <c>ServerApplication.CreateBuilder(root)</c> builds a server app. A concrete builder's
/// <see cref="Build"/> returns the platform application type (covariant return).
/// </para>
/// <para>
/// <b>Services (bring-your-own DI, <c>V01.01.03.24</c>).</b> Bring-your-own dependency injection over
/// <c>System.IServiceProvider</c> attaches through <see cref="Services"/> (register on the default Core
/// builder, or replace it with a container adapter via <see cref="UseServiceProviderBuilder"/>);
/// <see cref="Build"/> builds the provider and hands it to the application
/// (<see cref="IApplication.Services"/>). Component-tree <c>Provide</c>/<c>Inject</c> (the Vue-semantic
/// feature) stays untouched; app-level singleton wiring is what migrates to services.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IApplicationBuilder
{
    /// <summary>
    /// 
    /// </summary>
    IServiceContainer Services { get; }

    /// <summary>
    /// Builds the platform application and applies the recorded configuration in call order (upstream:
    /// the app returned by <c>createApp(root)</c> after <c>use</c>/<c>provide</c>/<c>component</c>).
    /// Concrete builders return their platform application type.
    /// </summary>
    /// <returns>The configured application; mount it through its platform mount entry point.</returns>
    IApplication Build();
}
