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
/// <b>Reserved services seam (R5, <c>V01.01.03.24</c>).</b> Bring-your-own dependency injection over
/// <c>System.IServiceProvider</c> attaches here: R5 adds an <c>IServiceProviderBuilder Services</c>
/// registration surface to this interface, and <see cref="Build"/> constructs the provider and hands
/// it to the application. R4 deliberately does not implement services — it only leaves this extension
/// point. Component-tree <c>Provide</c>/<c>Inject</c> (the Vue-semantic feature) stays; app-level
/// singleton wiring is what migrates to services.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IApplicationBuilder
{
    /// <summary>The root component the built application mounts.</summary>
    IComponentDefinition RootComponent { get; }

    /// <summary>The props passed to the root component, or null.</summary>
    VirtualNodeProperties? RootProperties { get; }

    /// <summary>
    /// Records a plugin to install on the built application (upstream: <c>app.use(plugin, options)</c>).
    /// Applied in call order at <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Use(IPlugin plugin, object? options = null);

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
    IApplicationBuilder Component(string name, IComponentDefinition definition);

    /// <summary>
    /// Records a named directive registration (upstream: <c>app.directive(name, directive)</c>).
    /// Applied at <see cref="Build"/>. Returns the builder for chaining.
    /// </summary>
    /// <param name="name">The directive name.</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder Directive(string name, IDirective directive);

    /// <summary>
    /// Records a callback that configures the built application's <see cref="ApplicationConfiguration"/>
    /// (upstream: setting <c>app.config</c> handlers before mount). Applied at <see cref="Build"/>.
    /// Returns the builder for chaining.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>This builder, for chaining.</returns>
    IApplicationBuilder ConfigureApplication(Action<ApplicationConfiguration> configure);

    /// <summary>
    /// Builds the platform application and applies the recorded configuration in call order (upstream:
    /// the app returned by <c>createApp(root)</c> after <c>use</c>/<c>provide</c>/<c>component</c>).
    /// Concrete builders return their platform application type.
    /// </summary>
    /// <returns>The configured application; mount it through its platform mount entry point.</returns>
    IApplication Build();
}
