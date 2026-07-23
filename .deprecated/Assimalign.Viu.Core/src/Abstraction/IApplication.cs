using System;

namespace Assimalign.Viu;

/// <summary>
/// The platform-neutral application contract — the C# port of the app object produced by
/// <c>createAppAPI(render)</c> in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/api/application.html),
/// reduced to the surface that does not need the platform node type: component/directive
/// registration, app-level <see cref="Provide{T}(InjectionKey{T}, T)"/>, plugin installation
/// (<see cref="Use"/>), the shared <see cref="Context"/>, and teardown. It is the face a plugin's
/// <see cref="IApplicationPlugin.InstallAsync"/> receives and the type an <see cref="IApplicationBuilder"/>
/// produces, so plugins and generic hosting code work against any platform.
/// <para>
/// <c>Mount</c> is deliberately absent: the container type is platform-specific (a browser CSS
/// selector or node handle, a Core <c>TNode</c>), so mounting lives on the concrete platform
/// applications (<see cref="Application{TNode}.Mount(TNode)"/>,
/// <c>BrowserApplication.MountAsync(string)</c>). Configuration lives on <see cref="Context"/>;
/// runtime introspection that needs no node type (<see cref="IsMounted"/>, <see cref="RootInstance"/>)
/// and <see cref="Unmount"/> stay here.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public interface IApplication
{
    /// <summary>
    /// The shared application context (upstream: <c>app._context</c> + <c>app.config</c>) — the root
    /// component/props, the app-level <see cref="IApplicationContext.ServicesProvider"/>, and the
    /// error/warn/performance handlers. Configuration lives here; the application object owns lifecycle.
    /// </summary>
    IApplicationContext Context { get; }

    /// <summary>Whether the application is currently mounted.</summary>
    bool IsMounted { get; }

    /// <summary>The root component instance after mounting, or null.</summary>
    ComponentInstance? RootInstance { get; }

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants of the root can resolve it
    /// by name — including <c>&lt;component :is="name"&gt;</c> dynamic components (upstream:
    /// <c>app.component(name, definition)</c>). Registering a duplicate name, or registering after
    /// mount, warns in dev. Returns the application for chaining.
    /// </summary>
    /// <param name="name">The component name (resolved case-insensitively at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication Component(string name, IComponent definition);

    /// <summary>
    /// Returns the component registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.component(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered definition, or null.</returns>
    IComponent? Component(string name);

    /// <summary>
    /// Registers a directive under <paramref name="name"/> so descendants of the root can resolve it
    /// by name (upstream: <c>app.directive(name, directive)</c>). Registering a duplicate name, or
    /// registering after mount, warns in dev. Returns the application for chaining.
    /// </summary>
    /// <param name="name">The directive name (resolved case-insensitively at render).</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication Directive(string name, IDirective directive);

    /// <summary>
    /// Returns the directive registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.directive(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered directive, or null.</returns>
    IDirective? Directive(string name);

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under the typed <paramref name="key"/> (upstream:
    /// <c>app.provide(key, value)</c>) — the final fallback in the inject lookup chain, resolved by
    /// any component without a nearer provider. Providing a duplicate key, or providing after mount,
    /// warns in dev. Returns the application for chaining.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication Provide<T>(InjectionKey<T> key, T value);

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under a string <paramref name="key"/> (upstream:
    /// <c>app.provide</c> with a string key). Returns the application for chaining.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication Provide(string key, object? value);

    /// <summary>
    /// Records <paramref name="plugin"/> for installation, exactly once (upstream:
    /// <c>app.use(plugin)</c>). A repeat <c>Use</c> of the same plugin instance is deduplicated with
    /// a dev warning; a plugin recorded after mount warns. Options are carried by the plugin's own
    /// constructor state, so there is no separate options argument. The plugin's asynchronous
    /// <see cref="IApplicationPlugin.InstallAsync"/> is awaited during the mount path (before platform
    /// initialization and the first render), so a plugin can register components, directives, and
    /// provides that the initial render sees. Returns the application for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <returns>This application, for chaining.</returns>
    IApplication Use(IApplicationPlugin plugin);

    /// <summary>
    /// Unmounts the application (upstream: <c>app.unmount</c>): runs component teardown lifecycles and
    /// releases the rendered tree. A no-op when not mounted.
    /// </summary>
    void Unmount();
}
