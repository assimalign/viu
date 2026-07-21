using System;
using System.Runtime.Versioning;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// A browser-mounted Viu application — the C# port of the app object
/// <c>createApp</c> returns in <c>@vue/runtime-dom</c>
/// (https://vuejs.org/api/application.html, <c>packages/runtime-dom/src/index.ts</c>). Wraps
/// the platform-agnostic <see cref="Application{TNode}"/> with browser container concerns:
/// selector resolution, clearing existing content before a non-hydrating client mount
/// (upstream parity), and full interop cleanup on <see cref="Unmount"/> — the bridge registry
/// returns to its pre-mount baseline. Create through
/// <see cref="BrowserRuntime.CreateApp"/>. Not thread-safe (browser main thread only).
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication
{
    private readonly Application<int> _application;
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;

    internal BrowserApplication(
        Application<int> application,
        BufferedBrowserNodeOperations? bufferedOperations = null,
        bool hydrate = false)
    {
        _application = application;
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
    }

    /// <summary>Whether the app is currently mounted.</summary>
    public bool IsMounted => _application.IsMounted;

    /// <summary>The root component instance after mounting, or null.</summary>
    public ComponentInstance? RootInstance => _application.RootInstance;

    /// <summary>
    /// The app-level configuration (upstream: <c>app.config</c>,
    /// https://vuejs.org/api/application.html#app-config) — the
    /// <see cref="ApplicationConfiguration.ErrorHandler"/> and
    /// <see cref="ApplicationConfiguration.WarnHandler"/>. Set its handlers before
    /// <see cref="Mount(string)"/>. Delegates to the wrapped <see cref="Application{TNode}.Config"/>.
    /// </summary>
    public ApplicationConfiguration Config => _application.Config;

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants of the root resolve it by
    /// name — including <c>&lt;component :is="name"&gt;</c> (upstream:
    /// <c>app.component(name, definition)</c>, https://vuejs.org/api/application.html#app-component).
    /// Registering a duplicate name, or registering after mount, warns in dev. Delegates to the
    /// wrapped <see cref="Application{TNode}"/>.
    /// </summary>
    /// <param name="name">The component name (resolved case-insensitively at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public BrowserApplication Component(string name, IComponentDefinition definition)
    {
        _application.Component(name, definition);
        return this;
    }

    /// <summary>
    /// Returns the component registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.component(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered definition, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public IComponentDefinition? Component(string name) => _application.Component(name);

    /// <summary>
    /// Registers a directive under <paramref name="name"/> so descendants resolve it by name through
    /// <see cref="Directives.ResolveDirective"/> (upstream: <c>app.directive(name, directive)</c>,
    /// https://vuejs.org/api/application.html#app-directive). Registering a duplicate name, or
    /// registering after mount, warns in dev. Delegates to the wrapped
    /// <see cref="Application{TNode}"/>.
    /// </summary>
    /// <param name="name">The directive name (resolved case-insensitively at render).</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directive"/> is null.</exception>
    public BrowserApplication Directive(string name, IDirective directive)
    {
        _application.Directive(name, directive);
        return this;
    }

    /// <summary>
    /// Returns the directive registered under <paramref name="name"/>, or null (upstream:
    /// <c>app.directive(name)</c> getter — an exact-name lookup).
    /// </summary>
    /// <param name="name">The registered name.</param>
    /// <returns>The registered directive, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public IDirective? Directive(string name) => _application.Directive(name);

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under the typed <paramref name="key"/> (upstream:
    /// <c>app.provide(key, value)</c>, https://vuejs.org/api/application.html#app-provide) — the
    /// final fallback in the inject lookup chain. Providing a duplicate key, or providing after
    /// mount, warns in dev. Delegates to the wrapped <see cref="Application{TNode}"/>.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public BrowserApplication Provide<T>(InjectionKey<T> key, T value)
    {
        _application.Provide(key, value);
        return this;
    }

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under a string <paramref name="key"/> (upstream:
    /// <c>app.provide</c> with a string key). Delegates to the wrapped
    /// <see cref="Application{TNode}"/>.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public BrowserApplication Provide(string key, object? value)
    {
        _application.Provide(key, value);
        return this;
    }

    /// <summary>
    /// Installs <paramref name="plugin"/> exactly once (upstream: <c>app.use(plugin, options)</c>,
    /// https://vuejs.org/api/application.html#app-use). A repeat <c>Use</c> of the same plugin
    /// instance is deduplicated with a dev warning; a plugin installed after mount warns. The
    /// plugin's <see cref="IVuePlugin{TNode}.Install"/> receives the wrapped
    /// <see cref="Application{TNode}"/>. Delegates to the wrapped application.
    /// </summary>
    /// <param name="plugin">The plugin to install (over the browser's <see cref="int"/> node handles).</param>
    /// <param name="options">Options passed to the plugin's install, or null.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is null.</exception>
    public BrowserApplication Use(IPlugin<int> plugin, object? options = null)
    {
        _application.Use(plugin, options);
        return this;
    }

    /// <summary>
    /// Resolves <paramref name="selector"/> and mounts there (upstream:
    /// <c>app.mount('#app')</c>). A selector matching nothing throws a
    /// <see cref="BrowserDomException"/> naming the selector.
    /// </summary>
    /// <param name="selector">The CSS selector of the container.</param>
    /// <returns>The root component instance.</returns>
    /// <exception cref="BrowserDomException">No element matches <paramref name="selector"/>.</exception>
    public ComponentInstance? Mount(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        return Mount(BrowserRuntime.QuerySelector(selector));
    }

    /// <summary>Mounts into an already-resolved container handle.</summary>
    /// <param name="containerHandle">The container's node handle.</param>
    /// <returns>The root component instance.</returns>
    public ComponentInstance? Mount(int containerHandle)
    {
        if (_application.IsMounted)
        {
            // Already mounted: delegate so the app warns and returns the existing instance (upstream parity).
            return _application.Mount(containerHandle);
        }
        // The container is a foreign node the bridge registered (a QuerySelector result); fold its handle
        // into the buffered handle counter so a buffered create never reuses it. Harmless in direct mode.
        _bufferedOperations?.ObserveForeignHandle(containerHandle);
        if (_hydrate)
        {
            // An app created with CreateSsrApp adopts the existing server-rendered content — the container
            // is NOT cleared (that content is what hydration reuses).
            return _application.Hydrate(containerHandle);
        }
        // Non-hydrating client mount clears existing container content (upstream parity); one interop call
        // that also releases any registered child handles.
        BrowserRuntime.ClearContainer(containerHandle);
        return _application.Mount(containerHandle);
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount()</c>): runs component teardown lifecycles,
    /// removes the rendered DOM, and releases every JS-side handle and listener the app
    /// created. In buffered mode the teardown mutations commit through the command buffer before the
    /// buffered operations are detached from the ambient scheduler/dispatch seams.
    /// </summary>
    public void Unmount()
    {
        _application.Unmount();
        _bufferedOperations?.Deactivate();
    }
}
