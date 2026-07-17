using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The browser entry point of Vuecs — the package that plays <c>@vue/runtime-dom</c>'s role
/// (https://github.com/vuejs/core/tree/main/packages/runtime-dom). Loads the bridge module
/// this package ships as a static web asset, then hands out renderers whose node-ops drive the
/// real DOM over int-handle interop. Single-threaded by design (browser main thread only);
/// not thread-safe.
/// </summary>
[SupportedOSPlatform("browser")]
public static class BrowserRuntime
{
    private static Task? _initialization;

    /// <summary>
    /// Loads the package's <c>vuecs-dom.js</c> bridge module and wires event dispatch. Idempotent —
    /// later calls await the same initialization. Must complete before any renderer is created.
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => _initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>
    /// Creates a renderer over the browser node-ops (upstream: <c>ensureRenderer()</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    public static Renderer<int> CreateRenderer()
    {
        EnsureInitialized();
        return RendererFactory.CreateRenderer(BrowserNodeOperations.Create());
    }

    /// <summary>
    /// Creates a browser application for <paramref name="rootComponent"/> (upstream:
    /// <c>createApp(rootComponent)</c>, https://vuejs.org/api/application.html) —
    /// <c>BrowserRuntime.CreateApp(root).Mount("#app")</c> is a Vuecs WASM app's whole
    /// bootstrap ([V01.01.04.04]).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <returns>The app; mount it by selector or handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    public static BrowserApplication CreateApp(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        EnsureInitialized();
        var renderer = RendererFactory.CreateRenderer(BrowserNodeOperations.Create());
        return new BrowserApplication(renderer.CreateApplication(rootComponent, rootProperties));
    }

    /// <summary>Clears a container's content in one interop call, releasing registered child handles.</summary>
    /// <param name="containerHandle">The container's node handle.</param>
    internal static void ClearContainer(int containerHandle)
        => BrowserNodeOperations.ClearElement(containerHandle);

    /// <summary>Resolves a selector to a node handle (e.g. the mount container).</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <exception cref="BrowserDomException">No node matches <paramref name="selector"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    public static int QuerySelector(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        EnsureInitialized();
        return BrowserDomBridge.QuerySelector(selector);
    }

    /// <summary>
    /// Leak diagnostics for the handle registries: JS node handles, JS listener maps, and
    /// C#-side listener entries. A mount/unmount cycle must return these to their prior sizes
    /// (the [V01.01.04.01] lifecycle contract).
    /// </summary>
    public static (int JsNodes, int JsListenerMaps, int DotnetListeners) GetRegistryDiagnostics()
    {
        EnsureInitialized();
        return BrowserNodeOperations.GetRegistryDiagnostics();
    }

    private static async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        // The module ships with this package as a static web asset; consumers get it under
        // /_content/<package id>/ (the module name doubles as the [JSImport] module key).
        await JSHost.ImportAsync(
            BrowserDomBridge.ModuleName,
            "/_content/Assimalign.Vue.RuntimeDom/vuecs-dom.js",
            cancellationToken);
        // The module resolves this assembly's exports and binds the single event-dispatch
        // entry point ([V01.01.04.03]).
        await BrowserDomBridge.InitializeModuleAsync();
    }

    private static void EnsureInitialized()
    {
        if (_initialization is not { IsCompletedSuccessfully: true })
        {
            throw new InvalidOperationException(
                "BrowserRuntime.InitializeAsync() must complete before using the DOM bridge.");
        }
    }
}
