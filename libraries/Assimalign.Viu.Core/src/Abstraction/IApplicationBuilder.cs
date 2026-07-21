using System;

namespace Assimalign.Viu;

/// <summary>
/// Configures and builds an application — the .NET-idiomatic bootstrap seam, shaped after
/// <c>Microsoft.AspNetCore.Builder.WebApplicationBuilder</c> (a builder collects configuration, then
/// <see cref="Build"/> produces the app). It is the Viu counterpart of composing an app before
/// <c>createApp(root)...mount()</c> in Vue (https://vuejs.org/api/application.html): the root
/// component and props are fixed at builder creation, and <see cref="Use"/>/<see cref="Provide{T}"/>/
/// <see cref="Component"/>/<see cref="Directive"/>/<see cref="ConfigureApplication"/> record
/// configuration that <see cref="Build"/> replays onto the freshly built application in call order.
/// <para>
/// Platform packages supply the entry points and concrete builders: the browser's
/// <c>BrowserApplication.CreateBuilder(root)</c> builds a browser app, the server renderer's
/// <c>ServerApplication.CreateBuilder(root)</c> builds a server app. A concrete builder's
/// <see cref="Build"/> returns the platform application type (covariant return).
/// </para>
/// <para>
/// <b>Services (bring-your-own DI, <c>V01.01.03.24</c>).</b> Bring-your-own dependency injection over
/// <c>System.IServiceProvider</c> attaches through <see cref="Services"/> (register on the default Core
/// <see cref="ServiceContainer"/>, or replace it with a container adapter via
/// <see cref="UseServiceContainer"/>); <see cref="Build"/> calls <see cref="IServiceContainer.Build"/>
/// exactly once and hands the provider to the application (<see cref="IApplicationContext.ServicesProvider"/>).
/// Component-tree <c>Provide</c>/<c>Inject</c> (the Vue-semantic feature) stays untouched; app-level
/// singleton wiring is what migrates to services.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IApplicationBuilder
{
    /// <summary>
    /// The bring-your-own dependency-injection registration surface ([V01.01.03.24]) — the Viu
    /// counterpart of <c>WebApplicationBuilder.Services</c>. Register services with the
    /// <see cref="ServiceContainerExtensions"/> helpers (<c>Services.AddSingleton(...)</c>);
    /// <see cref="Build"/> turns them into the application's
    /// <see cref="IApplicationContext.ServicesProvider"/>. The default is Core's AOT-safe
    /// <see cref="ServiceContainer"/>; replace it with a container adapter via
    /// <see cref="UseServiceContainer"/>.
    /// </summary>
    IServiceContainer Services { get; }

    /// <summary>
    /// Records a plugin to install on the built application (upstream: <c>app.use(plugin)</c>). Applied
    /// in call order at <see cref="Build"/> (the plugin's <see cref="IApplicationPlugin.InstallAsync"/>
    /// runs later, during the mount path). Returns the builder for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Use(IApplicationPlugin plugin);

    /// <summary>
    /// Records an app-level provide under the typed <paramref name="key"/> (upstream:
    /// <c>app.provide(key, value)</c>). Applied at <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Provide<T>(InjectionKey<T> key, T value);

    /// <summary>
    /// Records an app-level provide under a string <paramref name="key"/>. Applied at
    /// <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Provide(string key, object? value);

    /// <summary>
    /// Records a named component registration (upstream: <c>app.component(name, definition)</c>).
    /// Applied at <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="name">The component name.</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Component(string name, IComponent definition);

    /// <summary>
    /// Records a named directive registration (upstream: <c>app.directive(name, directive)</c>).
    /// Applied at <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="name">The directive name.</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Directive(string name, IDirective directive);

    /// <summary>
    /// Records a callback that configures the built application's <see cref="IApplicationContext"/>
    /// (upstream: setting <c>app.config</c> handlers before mount). Applied at <see cref="Build"/>.
    /// Returns the builder for chaining.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder ConfigureApplication(Action<IApplicationContext> configure);

    /// <summary>
    /// Replaces the <see cref="Services"/> container with a bring-your-own container — the Viu
    /// counterpart of <c>IHostBuilder.UseServiceProviderFactory</c> ([V01.01.03.24]). Call it before
    /// registering services (registrations go to the active container). At <see cref="Build"/> the
    /// container's <see cref="IServiceContainer.Build"/> result becomes
    /// <see cref="IApplicationContext.ServicesProvider"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="services">The bring-your-own service container.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder UseServiceContainer(IServiceContainer services);

    /// <summary>
    /// Records a callback that configures the <see cref="Services"/> registration surface — the fluent
    /// convenience over <c>Services</c> ([V01.01.03.24], compare
    /// <c>IHostBuilder.ConfigureServices</c>). Invoked immediately against the active service container.
    /// Returns the builder for chaining.
    /// </summary>
    /// <param name="configure">The service configuration callback.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder ConfigureServices(Action<IServiceContainer> configure);

    /// <summary>
    /// Builds the platform application, attaches the service provider, and applies the recorded
    /// configuration in call order (upstream: the app returned by <c>createApp(root)</c> after
    /// <c>use</c>/<c>provide</c>/<c>component</c>). Concrete builders return their platform application
    /// type; mount the returned app through its platform mount entry point.
    /// </summary>
    /// <returns>The configured application.</returns>
    IApplication Build();
}
