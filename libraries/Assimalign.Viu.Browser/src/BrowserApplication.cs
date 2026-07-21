using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// A browser-mounted Viu application — the C# port of the app object <c>createApp</c> returns in
/// <c>@vue/runtime-dom</c> (https://vuejs.org/api/application.html,
/// <c>packages/runtime-dom/src/index.ts</c>). It <b>extends</b> the platform-agnostic
/// <see cref="Application{TNode}"/> base (over <see cref="int"/> node handles) and owns the browser
/// concerns: it <b>overrides <see cref="Application{TNode}.OnInitializeAsync"/></b> to load the
/// <c>viu-dom.js</c> bridge module inside its own mount path — so there is no external
/// initialization pre-call (the reshape eliminated <c>BrowserRuntime.InitializeAsync()</c> from
/// consumer bootstrap, <c>V01.01.03.23</c>) — resolves CSS selectors, clears existing container
/// content before a non-hydrating client mount (upstream parity), and releases every JS-side handle
/// and listener on <see cref="Unmount"/>.
/// <para>
/// Build one with <see cref="CreateBuilder(IComponent, VirtualNodeProperties?, bool)"/>
/// (client mount) or <see cref="CreateSsrBuilder(IComponent, VirtualNodeProperties?)"/>
/// (hydrate server-rendered markup), then <c>await app.MountAsync("#app")</c>. Not thread-safe
/// (browser main thread only).
/// </para>
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication : Application<int>
{
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;
    // Seams (defaulting to the real bridge) so the DOM-dependent operations are substitutable in
    // DOM-free tests — the recording initialization seam pins "the bridge initializes exactly once
    // inside the mount path".
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly Action<int> _clearContainer;
    // Per-app cache of the initialization task and its completion, so OnInitializeAsync runs the
    // module import at most once no matter how many times the mount path awaits it.
    private Task? _initialization;
    private bool _initialized;

    internal BrowserApplication(
        Renderer<int> renderer,
        IComponent rootComponent,
        VirtualNodeProperties? rootProperties,
        BufferedBrowserNodeOperations? bufferedOperations = null,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null)
        : base(renderer, rootComponent, rootProperties)
    {
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
        _initialize = initialize ?? BrowserRuntime.EnsureBridgeAsync;
        _clearContainer = clearContainer ?? BrowserRuntime.ClearContainer;
    }

    /// <summary>
    /// Whether this app hydrates existing server-rendered markup (created through
    /// <see cref="CreateSsrBuilder"/>) rather than mounting fresh.
    /// </summary>
    public bool IsHydrating => _hydrate;

    /// <summary>
    /// Creates a builder for a browser application that mounts <paramref name="rootComponent"/>
    /// fresh — the .NET-idiomatic bootstrap (compare <c>WebApplication.CreateBuilder</c>) that
    /// replaces upstream's <c>createApp(rootComponent)</c>. Configure plugins/provides on the
    /// builder, <c>Build()</c> the app, then <c>await app.MountAsync("#app")</c>.
    /// <para>
    /// Set <paramref name="useCommandBuffer"/> to run the renderer over the interop command buffer
    /// ([V01.01.04.05]): node-ops serialize into a shared binary frame that a single interop call
    /// applies per scheduler flush instead of one call per mutation. It is behaviorally invisible —
    /// buffered and direct modes produce byte-identical DOM. Default is direct.
    /// </para>
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <param name="useCommandBuffer">Whether to batch node-ops through the command buffer.</param>
    /// <returns>A builder whose <see cref="BrowserApplicationBuilder.Build"/> produces the app.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public static BrowserApplicationBuilder CreateBuilder(
        IComponent rootComponent,
        VirtualNodeProperties? rootProperties = null,
        bool useCommandBuffer = false)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return new BrowserApplicationBuilder(rootComponent, rootProperties, useCommandBuffer, hydrate: false);
    }

    /// <summary>
    /// Creates a builder for a browser application that <b>hydrates</b> existing server-rendered
    /// markup rather than mounting fresh — the C# port of <c>createSSRApp(rootComponent)</c> in
    /// <c>@vue/runtime-dom</c> (https://vuejs.org/guide/scaling-up/ssr.html#client-hydration). The
    /// built app's <see cref="MountAsync(string, CancellationToken)"/> adopts the container's server
    /// DOM (attaching listeners and component instances, reconciling only dynamic bindings) instead
    /// of clearing and recreating it; a server/client mismatch recovers per subtree without crashing.
    /// The container must already hold the markup the server renderer produced for the same root.
    /// </summary>
    /// <param name="rootComponent">The root component definition (the same one rendered on the server).</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <returns>A builder whose <see cref="BrowserApplicationBuilder.Build"/> produces the hydrating app.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public static BrowserApplicationBuilder CreateSsrBuilder(
        IComponent rootComponent,
        VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return new BrowserApplicationBuilder(rootComponent, rootProperties, useCommandBuffer: false, hydrate: true);
    }

    /// <summary>
    /// Resolves <paramref name="selector"/> and mounts there (upstream: <c>app.mount('#app')</c>),
    /// initializing the browser bridge inside this mount path first. A selector matching nothing
    /// throws a <see cref="BrowserDomException"/> naming the selector.
    /// </summary>
    /// <param name="selector">The CSS selector of the container.</param>
    /// <param name="cancellationToken">Cancels the bridge initialization.</param>
    /// <returns>The root component instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="selector"/> is null or empty.</exception>
    /// <exception cref="BrowserDomException">No element matches <paramref name="selector"/>.</exception>
    public async Task<ComponentInstance?> MountAsync(string selector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        // Initialize before resolving the selector — QuerySelector needs the bridge loaded.
        if (!IsMounted)
        {
            await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        return Mount(BrowserRuntime.QuerySelector(selector));
    }

    /// <summary>
    /// Synchronously mounts into an already-resolved container handle. Advanced: the browser bridge
    /// must already be initialized (use <see cref="MountAsync(string, CancellationToken)"/> or
    /// <see cref="Application{TNode}.MountAsync(int, CancellationToken)"/> otherwise). Clears the
    /// container for a client mount, or adopts its content when hydrating.
    /// </summary>
    /// <param name="container">The container's node handle.</param>
    /// <returns>The root component instance.</returns>
    /// <exception cref="InvalidOperationException">The bridge is not initialized and this app has not initialized it.</exception>
    public override ComponentInstance? Mount(int container)
    {
        if (!_initialized && !BrowserRuntime.IsBridgeInitialized)
        {
            throw new InvalidOperationException(
                "The Viu browser bridge is not initialized. Mount with MountAsync so the bridge initializes "
                + "in the mount path (the reshape removed the external BrowserRuntime.InitializeAsync() pre-call).");
        }
        if (IsMounted)
        {
            // Already mounted: delegate so the base warns and returns the existing instance (upstream parity).
            return base.Mount(container);
        }
        // The container is a foreign node the bridge registered (a QuerySelector result); fold its handle
        // into the buffered handle counter so a buffered create never reuses it. Harmless in direct mode.
        _bufferedOperations?.ObserveForeignHandle(container);
        if (_hydrate)
        {
            // A hydrating app adopts the existing server-rendered content — the container is NOT cleared.
            return Hydrate(container);
        }
        // Non-hydrating client mount clears existing container content (upstream parity); one interop call
        // that also releases any registered child handles.
        _clearContainer(container);
        return base.Mount(container);
    }

    /// <summary>
    /// Unmounts the app (upstream: <c>app.unmount()</c>): runs component teardown lifecycles,
    /// removes the rendered DOM, and releases every JS-side handle and listener the app created. In
    /// buffered mode the teardown mutations commit through the command buffer before the buffered
    /// operations are detached from the ambient scheduler/dispatch seams.
    /// </summary>
    public override void Unmount()
    {
        base.Unmount();
        _bufferedOperations?.Deactivate();
    }

    /// <summary>
    /// Loads the <c>viu-dom.js</c> bridge module for this mount path (upstream has no analog — the
    /// JS runtime is always present). Cached per app so the module import runs at most once no matter
    /// how many times the mount path awaits it; the shared bridge itself initializes once per process.
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    /// <returns>A task that completes when the bridge is ready.</returns>
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        => _initialization ??= InitializeBridgeAsync(cancellationToken);

    private async Task InitializeBridgeAsync(CancellationToken cancellationToken)
    {
        await _initialize(cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    // Builds the app over the direct or command-buffered browser node-ops; the builder calls this.
    internal static BrowserApplication Create(
        IComponent rootComponent,
        VirtualNodeProperties? rootProperties,
        bool useCommandBuffer,
        bool hydrate)
    {
        if (useCommandBuffer)
        {
            var bufferedOperations = BufferedBrowserNodeOperations.CreateProduction();
            var bufferedRenderer = RendererFactory.CreateRenderer(bufferedOperations.Create());
            return new BrowserApplication(bufferedRenderer, rootComponent, rootProperties, bufferedOperations, hydrate);
        }
        var renderer = RendererFactory.CreateRenderer(BrowserNodeOperations.Create());
        return new BrowserApplication(renderer, rootComponent, rootProperties, bufferedOperations: null, hydrate);
    }
}
