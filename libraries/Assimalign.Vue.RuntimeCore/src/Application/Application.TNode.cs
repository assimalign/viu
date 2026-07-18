using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The application shell — the C# port of the app object produced by <c>createAppAPI(render)</c>
/// in <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/apiCreateApp.ts</c>,
/// https://vuejs.org/api/application.html): one root component mounted into one container and
/// unmounted as a whole, over a shared <see cref="ApplicationContext"/> that carries the component
/// registry, app-level provides, and <see cref="ApplicationConfiguration"/> to every descendant.
/// <see cref="Component(string, IComponentDefinition)"/>, <see cref="Provide{T}"/>, and
/// <see cref="Use(IPlugin{TNode}, object?)"/> configure the app before mounting; registering
/// after <see cref="Mount"/> warns (upstream parity). Platform packages wrap this with container
/// resolution (the browser's <c>CreateApp(...).Mount("#app")</c> is [V01.01.04.04]).
/// <para>
/// <c>app.config.globalProperties</c> is deliberately excluded in favor of typed app-level
/// provide/inject — see <see cref="ApplicationConfiguration"/> and the founding ADR
/// ([V01.01.13.01]).
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
public sealed class Application<TNode>
    where TNode : notnull
{
    private readonly Renderer<TNode> _renderer;
    private readonly IComponentDefinition _rootComponent;
    private readonly VirtualNodeProperties? _rootProperties;
    private readonly ApplicationContext _context = new();
    private HashSet<object>? _installedPlugins;
    private VirtualNode? _rootVirtualNode;
    private TNode? _container;
    private Action<string>? _previousWarnSink;
    private bool _warnSinkInstalled;

    internal Application(Renderer<TNode> renderer, IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties)
    {
        _renderer = renderer;
        _rootComponent = rootComponent;
        _rootProperties = rootProperties;
    }

    /// <summary>Whether the app is currently mounted.</summary>
    public bool IsMounted { get; private set; }

    /// <summary>The root component instance after mounting, or null.</summary>
    public ComponentInstance? RootInstance => _rootVirtualNode?.Component as ComponentInstance;

    /// <summary>
    /// The app-level configuration (upstream: <c>app.config</c>) — the error handler, warn
    /// handler, and performance flag. Set its handlers before <see cref="Mount"/>.
    /// </summary>
    public ApplicationConfiguration Config => _context.Config;

    /// <summary>The shared application context (internal seam for the platform packages and test utilities).</summary>
    internal ApplicationContext Context => _context;

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants of the root can resolve
    /// it by name — including <c>&lt;component :is="name"&gt;</c> dynamic components (upstream:
    /// <c>app.component(name, definition)</c>). Registering a duplicate name, or registering after
    /// mount, warns in dev. Returns the app for chaining.
    /// </summary>
    /// <param name="name">The component name (resolved case-insensitively to camelCase/PascalCase forms at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public Application<TNode> Component(string name, IComponentDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(definition);
        WarnIfMounted(nameof(Component));
        if (_context.Components.ContainsKey(name))
        {
            RuntimeWarnings.Warn($"Component \"{name}\" has already been registered in target app.");
        }
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
    /// Registers a directive under <paramref name="name"/> so descendants of the root can resolve
    /// it by name through <see cref="Directives.ResolveDirective"/> (upstream:
    /// <c>app.directive(name, directive)</c>). Registering a duplicate name, or registering after
    /// mount, warns in dev. Returns the app for chaining.
    /// </summary>
    /// <param name="name">The directive name (resolved case-insensitively at render).</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directive"/> is null.</exception>
    public Application<TNode> Directive(string name, IDirective directive)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(directive);
        WarnIfMounted(nameof(Directive));
        if (_context.Directives.ContainsKey(name))
        {
            RuntimeWarnings.Warn($"Directive \"{name}\" has already been registered in target app.");
        }
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
    /// <c>app.provide(key, value)</c>) — the final fallback in the inject lookup chain
    /// ([V01.01.03.10]), resolved by any component that does not have a nearer provider. Providing
    /// a duplicate key, or providing after mount, warns in dev. Returns the app for chaining.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public Application<TNode> Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ProvideCore(key, value);
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
    public Application<TNode> Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ProvideCore(key, value);
        return this;
    }

    /// <summary>
    /// Installs <paramref name="plugin"/> exactly once (upstream: <c>app.use(plugin, options)</c>).
    /// A repeat <c>Use</c> of the same plugin instance is deduplicated with a dev warning; a plugin
    /// installed after mount warns. The plugin's <see cref="IPlugin{TNode}.Install"/> receives
    /// this app, through which it registers components, directives, and provides. Returns the app
    /// for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is null.</exception>
    public Application<TNode> Use(IPlugin<TNode> plugin, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        WarnIfMounted(nameof(Use));
        // Dedupe by reference identity (upstream: installedPlugins.has(plugin)).
        _installedPlugins ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!_installedPlugins.Add(plugin))
        {
            RuntimeWarnings.Warn("Plugin has already been applied to target app.");
            return this;
        }
        plugin.Install(this, options);
        return this;
    }

    /// <summary>
    /// Mounts the root component into <paramref name="container"/> (upstream: <c>app.mount</c>),
    /// attaching the app context so descendants resolve app-level provides, registered components,
    /// and config. A second call warns and no-ops, returning the existing instance (upstream
    /// parity).
    /// </summary>
    /// <param name="container">The platform container node.</param>
    /// <returns>The root component instance.</returns>
    public ComponentInstance? Mount(TNode container)
    {
        if (IsMounted)
        {
            RuntimeWarnings.Warn(
                "App has already been mounted. Create a new app instance to mount again, or call Unmount() first.");
            return RootInstance;
        }
        // Route dev warnings to the configured handler for the mounted lifetime (upstream consults
        // appContext.config.warnHandler at warn time; the seam here is the message-based sink).
        if (_context.Config.WarnHandler is { } warnHandler)
        {
            _previousWarnSink = RuntimeWarnings.Sink;
            RuntimeWarnings.Sink = warnHandler;
            _warnSinkInstalled = true;
        }
        _rootVirtualNode = VirtualNodeFactory.Component(_rootComponent, _rootProperties);
        _rootVirtualNode.AppContext = _context;
        _renderer.Render(_rootVirtualNode, container);
        _container = container;
        IsMounted = true;
        return RootInstance;
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount</c>): runs the component teardown lifecycles,
    /// removes the rendered tree from the container, and restores the warning sink.
    /// </summary>
    public void Unmount()
    {
        if (!IsMounted)
        {
            return;
        }
        _renderer.Render(null, _container!);
        _rootVirtualNode = null;
        _container = default;
        IsMounted = false;
        if (_warnSinkInstalled)
        {
            RuntimeWarnings.Sink = _previousWarnSink!;
            _previousWarnSink = null;
            _warnSinkInstalled = false;
        }
    }

    private void ProvideCore(object key, object? value)
    {
        WarnIfMounted(nameof(Provide));
        if (_context.Provides.ContainsKey(key))
        {
            RuntimeWarnings.Warn(
                $"App already provides property with key \"{key}\". It will be overwritten with the new value.");
        }
        _context.Provides[key] = value;
    }

    private void WarnIfMounted(string api)
    {
        if (IsMounted)
        {
            RuntimeWarnings.Warn(
                $"{api}() cannot be called on an already mounted app — the registration will not affect the "
                + "rendered tree. Configure the app before Mount().");
        }
    }
}
