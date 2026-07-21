using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// A host-agnostic server-rendering application — the C# port of the app object
/// <c>createSSRApp(RootComponent)</c> produces (<c>@vue/runtime-core</c>'s <c>createAppAPI</c>,
/// https://vuejs.org/api/application.html), pared to what server rendering consumes: a root component,
/// its root props, and the app-level registries and provides every descendant resolves against.
/// <para>
/// This is deliberately <b>not</b> the DOM-bound <see cref="Application{TNode}"/>: that type is
/// inseparable from a DOM renderer (it is generic over the platform node and owns a
/// <c>Renderer&lt;TNode&gt;</c>), whereas founding decision 7 requires the server renderer to run on a
/// plain .NET host with no DOM/interop dependency. So the SSR app carries the same
/// component/directive/provide/plugin surface over the shared <see cref="ApplicationContext"/> without
/// the renderer, implementing the platform-neutral <see cref="IApplication"/> so plugins and the
/// builder work against it uniformly. It never mounts (SSR renders to a string per request), so
/// <see cref="IsMounted"/> is always false, <see cref="RootInstance"/> null, and <see cref="Unmount"/>
/// a no-op. Per-request discipline is the caller's: construct a fresh <see cref="ServerApplication"/>
/// per request (see <see cref="CreateBuilder"/>) so no reactive state crosses requests (the host DI
/// wiring is the server adaptor's concern, [V01.01.07.04]).
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ServerApplication : IApplication
{
    private readonly ApplicationContext _context = new();
    private HashSet<object>? _installedPlugins;

    /// <summary>Creates a server app for <paramref name="rootComponent"/> with optional root props.</summary>
    /// <param name="rootComponent">The root component definition (upstream: <c>createSSRApp</c>'s argument).</param>
    /// <param name="rootProperties">The props passed to the root component, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public ServerApplication(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        RootComponent = rootComponent;
        RootProperties = rootProperties;
    }

    /// <summary>
    /// Creates a builder for a server application that renders <paramref name="rootComponent"/> — the
    /// .NET-idiomatic bootstrap aligned with the browser's builder. Configure plugins/provides on the
    /// builder, <c>Build()</c> the app, then pass it to
    /// <see cref="ServerRenderer.RenderToStringAsync(ServerApplication, SsrContext?, System.Threading.CancellationToken)"/>.
    /// Build a fresh app per request (no cross-request state).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <returns>A builder whose <see cref="ServerApplicationBuilder.Build"/> produces the app.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public static ServerApplicationBuilder CreateBuilder(
        IComponentDefinition rootComponent,
        VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return new ServerApplicationBuilder(rootComponent, rootProperties);
    }

    /// <summary>Always false — a server application renders to a string and never mounts.</summary>
    public bool IsMounted => false;

    /// <summary>Always null — a server application has no live mounted root instance.</summary>
    public ComponentInstance? RootInstance => null;

    /// <summary>The root component definition.</summary>
    public IComponentDefinition RootComponent { get; }

    /// <summary>The root component's props, or null.</summary>
    public VirtualNodeProperties? RootProperties { get; }

    /// <summary>
    /// The app-level configuration (upstream: <c>app.config</c>) — the error and warn handlers the SSR
    /// error path consults. Configure it before rendering.
    /// </summary>
    public ApplicationConfiguration Config => _context.Config;

    /// <summary>The shared application context threaded onto the root vnode at render.</summary>
    internal ApplicationContext Context => _context;

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants resolve it by name, including
    /// <c>&lt;component :is&gt;</c> (upstream: <c>app.component(name, definition)</c>). Returns the app for
    /// chaining.
    /// </summary>
    /// <param name="name">The component name (resolved raw/camelCase/PascalCase at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public ServerApplication Component(string name, IComponentDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(definition);
        _context.Components[name] = definition;
        return this;
    }

    /// <summary>
    /// Returns the component registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.component(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered definition, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public IComponentDefinition? Component(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _context.Components.TryGetValue(name, out var definition) ? definition : null;
    }

    /// <summary>
    /// Registers a directive under <paramref name="name"/> (upstream: <c>app.directive(name, directive)</c>).
    /// Returns the app for chaining. (Directive server-side props via <c>getSSRProps</c> are future work;
    /// registration is provided so name resolution succeeds.)
    /// </summary>
    /// <param name="name">The directive name.</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directive"/> is null.</exception>
    public ServerApplication Directive(string name, IDirective directive)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(directive);
        _context.Directives[name] = directive;
        return this;
    }

    /// <summary>
    /// Returns the directive registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.directive(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered directive, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public IDirective? Directive(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _context.Directives.TryGetValue(name, out var directive) ? directive : null;
    }

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under the typed <paramref name="key"/> (upstream:
    /// <c>app.provide(key, value)</c>) — the final fallback in the inject lookup chain. Returns the app for
    /// chaining.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ServerApplication Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _context.Provides[key] = value;
        return this;
    }

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under a string <paramref name="key"/> (upstream:
    /// <c>app.provide</c> with a string key). Returns the app for chaining.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public ServerApplication Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _context.Provides[key] = value;
        return this;
    }

    /// <summary>
    /// Installs <paramref name="plugin"/> exactly once (upstream: <c>app.use(plugin, options)</c>),
    /// so app-level plugins (a Pinia-style store registry, for example) register their provides into
    /// the shared context before rendering. A repeat <c>Use</c> of the same instance is deduplicated.
    /// Returns the app for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is null.</exception>
    public ServerApplication Use(IPlugin plugin, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _installedPlugins ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (_installedPlugins.Add(plugin))
        {
            plugin.Install(this, options);
        }
        return this;
    }

    /// <summary>
    /// No-op — a server application renders to a string per request and never mounts, so there is
    /// nothing to tear down (satisfies <see cref="IApplication"/>). Isolate requests by building a
    /// fresh <see cref="ServerApplication"/> per request instead of unmounting a shared one.
    /// </summary>
    public void Unmount()
    {
    }

    IApplication IApplication.Component(string name, IComponentDefinition definition) => Component(name, definition);

    IApplication IApplication.Directive(string name, IDirective directive) => Directive(name, directive);

    IApplication IApplication.Provide<T>(InjectionKey<T> key, T value) => Provide(key, value);

    IApplication IApplication.Provide(string key, object? value) => Provide(key, value);

    IApplication IApplication.Use(IPlugin plugin, object? options) => Use(plugin, options);
}
