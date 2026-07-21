using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// The application shell — the C# port of the app object produced by <c>createAppAPI(render)</c>
/// in <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/apiCreateApp.ts</c>,
/// https://vuejs.org/api/application.html): one root component mounted into one container and
/// unmounted as a whole, over a shared <see cref="ApplicationContext"/> that carries the component
/// registry, app-level provides, and <see cref="ApplicationConfiguration"/> to every descendant.
/// <see cref="Component(string, IComponentDefinition)"/>, <see cref="Provide{T}"/>, and
/// <see cref="Use(IPlugin, object?)"/> configure the app before mounting; registering after
/// <see cref="Mount"/> warns (upstream parity).
/// <para>
/// This is the platform-neutral, <b>extensible base</b> (the reshape's "application base",
/// <c>V01.01.03.23</c>): it implements the node-type-agnostic <see cref="IApplication"/> and exposes
/// <c>virtual</c>/<c>protected</c> lifecycle seams — <see cref="OnInitializeAsync"/> (the async
/// initialization step in the mount path a browser needs for its awaited module imports),
/// <see cref="CreateRootVirtualNode"/>, <see cref="InstallPlugin"/>, <see cref="Mount(TNode)"/>, and
/// <see cref="Unmount"/>. It is not marked <c>abstract</c> because the default renderer-bound app
/// (Core, the Testing renderer, custom renderers) is created directly by
/// <see cref="Renderer{TNode}.CreateApplication"/> and mounts as-is; platform packages that need
/// awaited initialization (the browser) derive and override the seams. The constructor stays
/// <c>internal</c>, so the type is opaque and un-subclassable outside the framework.
/// </para>
/// <para>
/// <c>app.config.globalProperties</c> is deliberately excluded in favor of typed app-level
/// provide/inject — see <see cref="ApplicationConfiguration"/> and the founding ADR
/// ([V01.01.13.01]). Platform packages wrap this with container resolution and a builder entry point
/// (the browser's <c>BrowserApplication.CreateBuilder(root).Build().MountAsync("#app")</c>).
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
public class Application<TNode> : IApplication, IDisposable
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
    private bool _servicesDisposed;

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

    /// <summary>
    /// The application's dependency-injection provider ([V01.01.03.24]) — the
    /// <see cref="IServiceProvider"/> an <see cref="IApplicationBuilder"/> built and attached, reachable
    /// from component <c>Setup</c> through <see cref="ComponentInstance.Services"/> and the
    /// <see cref="DependencyInjection.GetService{T}()"/> composition functions. Null when the app was
    /// created directly through <see cref="Renderer{TNode}.CreateApplication"/> (no builder). The app
    /// owns this provider and disposes it (if <see cref="IDisposable"/>) in <see cref="Dispose"/>.
    /// </summary>
    public IServiceProvider? Services => _context.Services;

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
    /// installed after mount warns. The plugin's <see cref="IPlugin.Install"/> receives this app
    /// (through the <see cref="InstallPlugin"/> seam), through which it registers components,
    /// directives, and provides. Returns the app for chaining.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is null.</exception>
    public Application<TNode> Use(IPlugin plugin, object? options = null)
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
        InstallPlugin(plugin, options);
        return this;
    }

    /// <summary>
    /// Mounts the root component into <paramref name="container"/> synchronously (upstream:
    /// <c>app.mount</c>), attaching the app context so descendants resolve app-level provides,
    /// registered components, and config. A second call warns and no-ops, returning the existing
    /// instance (upstream parity).
    /// <para>
    /// This is the synchronous mount for platforms whose <see cref="OnInitializeAsync"/> is a no-op
    /// (Core, the Testing renderer, custom renderers). Platforms with awaited initialization override
    /// this seam and expose an async entry (the browser's <c>MountAsync</c>); prefer
    /// <see cref="MountAsync"/> for the initialization-aware path.
    /// </para>
    /// </summary>
    /// <param name="container">The platform container node.</param>
    /// <returns>The root component instance.</returns>
    public virtual ComponentInstance? Mount(TNode container) => MountCore(container, hydrate: false);

    /// <summary>
    /// Runs the overridable <see cref="OnInitializeAsync"/> step (a no-op by default; the browser
    /// awaits its module imports here), then mounts into <paramref name="container"/>. This is the
    /// mount path the reshape's builder bootstrap uses (builder -&gt; <c>Build()</c> -&gt;
    /// <c>MountAsync</c>), so no separate runtime initialization pre-call is needed. A second call
    /// warns and no-ops (initialization does not re-run).
    /// </summary>
    /// <param name="container">The platform container node.</param>
    /// <param name="cancellationToken">Cancels the initialization step.</param>
    /// <returns>The root component instance.</returns>
    public async Task<ComponentInstance?> MountAsync(TNode container, CancellationToken cancellationToken = default)
    {
        if (!IsMounted)
        {
            await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        return Mount(container);
    }

    /// <summary>
    /// Mounts by hydrating the existing server-rendered content of <paramref name="container"/>
    /// (upstream: the <c>mount</c> of an app created with <c>createSSRApp</c>,
    /// <c>packages/runtime-core/src/renderer.ts</c> — <c>createAppAPI(render, hydrate)</c>,
    /// https://vuejs.org/guide/scaling-up/ssr.html#client-hydration). The root component adopts the
    /// server DOM instead of recreating it; a server/client mismatch recovers per subtree without
    /// crashing. Platform packages surface this as their <c>CreateSsrBuilder(...).Build().MountAsync(...)</c>
    /// entry. A second call warns and no-ops.
    /// </summary>
    /// <param name="container">The container holding the server-rendered markup.</param>
    /// <returns>The root component instance.</returns>
    internal ComponentInstance? Hydrate(TNode container) => MountCore(container, hydrate: true);

    private ComponentInstance? MountCore(TNode container, bool hydrate)
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
        _rootVirtualNode = CreateRootVirtualNode();
        _rootVirtualNode.AppContext = _context;
        if (hydrate)
        {
            _renderer.Hydrate(_rootVirtualNode, container);
        }
        else
        {
            _renderer.Render(_rootVirtualNode, container);
        }
        _container = container;
        IsMounted = true;
        return RootInstance;
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount</c>): runs the component teardown lifecycles,
    /// removes the rendered tree from the container, and restores the warning sink. A no-op when not
    /// mounted. Platforms with extra teardown (the browser's interop-handle cleanup) override and
    /// call <c>base.Unmount()</c>.
    /// </summary>
    public virtual void Unmount()
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

    /// <summary>
    /// Disposes the app: unmounts it (a <c>using</c>-friendly alias for <see cref="Unmount"/>) and then
    /// disposes the owned <see cref="Services"/> provider if it is <see cref="IDisposable"/>, cascading
    /// to its owned disposable singleton/scoped services ([V01.01.03.24]). Idempotent — the provider is
    /// disposed at most once, and unmount is a no-op when not mounted.
    /// </summary>
    public void Dispose()
    {
        Unmount();
        if (!_servicesDisposed)
        {
            _servicesDisposed = true;
            (_context.Services as IDisposable)?.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The overridable asynchronous initialization step run by <see cref="MountAsync"/> before the
    /// first mount — the seam that lets a platform await work that must complete before rendering
    /// (the browser awaits its <c>viu-dom.js</c> module import here, [V01.01.04.03]). The default is
    /// a completed task (no initialization), so the synchronous <see cref="Mount(TNode)"/> path is
    /// unaffected. Implementations should be idempotent (cache the work) so repeated mount attempts
    /// initialize at most once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initialization work.</param>
    /// <returns>A task that completes when the platform is ready to render.</returns>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Creates the root virtual node mounted into the container (upstream: the root vnode built in
    /// <c>app.mount</c>). The default builds a component vnode from the configured root component and
    /// props; a platform may override to customize root-vnode creation.
    /// </summary>
    /// <returns>The root virtual node.</returns>
    protected virtual VirtualNode CreateRootVirtualNode()
        => VirtualNodeFactory.Component(_rootComponent, _rootProperties);

    /// <summary>
    /// Installs <paramref name="plugin"/> into this application (upstream:
    /// <c>plugin.install(app, options)</c>) — the seam <see cref="Use"/> calls once per plugin
    /// instance, after the dedupe/after-mount guards. The default invokes
    /// <see cref="IPlugin.Install"/> against this app.
    /// </summary>
    /// <param name="plugin">The plugin to install.</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    protected virtual void InstallPlugin(IPlugin plugin, object? options) => plugin.Install(this, options);

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

    IApplication IApplication.Component(string name, IComponentDefinition definition) => Component(name, definition);

    IApplication IApplication.Directive(string name, IDirective directive) => Directive(name, directive);

    IApplication IApplication.Provide<T>(InjectionKey<T> key, T value) => Provide(key, value);

    IApplication IApplication.Provide(string key, object? value) => Provide(key, value);

    IApplication IApplication.Use(IPlugin plugin, object? options) => Use(plugin, options);
}
