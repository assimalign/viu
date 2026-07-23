using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The browser entry point of Viu — the package that plays <c>@vue/runtime-dom</c>'s role
/// (https://github.com/vuejs/core/tree/main/packages/runtime-dom). Loads the bridge module
/// this package ships as a static web asset, then hands out renderers whose node-ops drive the
/// real DOM over int-handle interop. Single-threaded by design (browser main thread only);
/// not thread-safe.
/// <para>
/// Normal app bootstrap does <b>not</b> call this type: build a <c>BrowserApplication</c> with
/// <see cref="BrowserApplication.CreateBuilder(IComponent, bool)"/>
/// and mount it with <c>MountAsync</c>, which owns the bridge initialization internally (the reshape
/// eliminated the external initialization pre-call, <c>V01.01.03.23</c>). The members here are the
/// low-level primitives for advanced scenarios — a bare renderer, selector resolution, or the
/// handle-leak diagnostics — that operate without an application.
/// </para>
/// </summary>
[SupportedOSPlatform("browser")]
public static class BrowserRuntime
{
    private static Task? _initialization;

    /// <summary>
    /// Loads the package's <c>viu-dom.js</c> bridge module and wires event dispatch. Idempotent —
    /// later calls await the same initialization.
    /// <para>
    /// <b>Advanced/low-level.</b> A normal app does not call this: <c>BrowserApplication.MountAsync</c>
    /// runs the same initialization internally through its mount path. Use this only for the bare-
    /// primitive scenarios (<see cref="CreateRenderer"/>, <see cref="QuerySelector"/>, the leak
    /// diagnostics) that need the bridge without an application.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => EnsureBridgeAsync(cancellationToken);

    /// <summary>
    /// Ensures the <c>viu-dom.js</c> bridge module is loaded and event dispatch is wired, caching the
    /// initialization so it runs at most once per process no matter how many applications mount. This
    /// is the seam <see cref="BrowserApplication.OnInitializeAsync"/> awaits inside its mount path.
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    internal static Task EnsureBridgeAsync(CancellationToken cancellationToken = default)
        => _initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>Whether the <c>viu-dom.js</c> bridge has finished initializing.</summary>
    internal static bool IsBridgeInitialized => _initialization is { IsCompletedSuccessfully: true };

    /// <summary>
    /// Creates a renderer over the browser node-ops (upstream: <c>ensureRenderer()</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge has not been initialized (call <see cref="InitializeAsync"/>).</exception>
    public static Renderer<int> CreateRenderer()
    {
        EnsureBridgeInitialized();
        return RendererFactory.CreateRenderer(BrowserNodeOperations.Create());
    }

    /// <summary>Clears a container's content in one interop call, releasing registered child handles.</summary>
    /// <param name="containerHandle">The container's node handle.</param>
    internal static void ClearContainer(int containerHandle)
        => BrowserNodeOperations.ClearElement(containerHandle);

    /// <summary>Resolves a selector to a node handle (e.g. the mount container).</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <exception cref="BrowserDomException">No node matches <paramref name="selector"/>.</exception>
    /// <exception cref="InvalidOperationException">The bridge has not been initialized.</exception>
    public static int QuerySelector(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        EnsureBridgeInitialized();
        return BrowserDomBridge.QuerySelector(selector);
    }

    /// <summary>
    /// Leak diagnostics for the handle registries: JS node handles, JS listener maps, and
    /// C#-side listener entries. A mount/unmount cycle must return these to their prior sizes
    /// (the [V01.01.04.01] lifecycle contract).
    /// </summary>
    public static (int JsNodes, int JsListenerMaps, int DotnetListeners) GetRegistryDiagnostics()
    {
        EnsureBridgeInitialized();
        return BrowserNodeOperations.GetRegistryDiagnostics();
    }

    private static async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        // The module ships with this package as a static web asset; consumers get it under
        // /_content/<package id>/ (the module name doubles as the [JSImport] module key).
        await JSHost.ImportAsync(
            BrowserDomBridge.ModuleName,
            "/_content/Assimalign.Viu.Browser/viu-dom.js",
            cancellationToken);
        // The module resolves this assembly's exports and binds the single event-dispatch
        // entry point ([V01.01.04.03]).
        await BrowserDomBridge.InitializeModuleAsync();
    }

    /// <summary>
    /// Throws when the bridge has not finished initializing — the guard for the synchronous
    /// primitives and the synchronous <c>BrowserApplication.Mount(int)</c> advanced path.
    /// </summary>
    internal static void EnsureBridgeInitialized()
    {
        if (!IsBridgeInitialized)
        {
            throw new InvalidOperationException(
                "The Viu browser bridge is not initialized. Mount a BrowserApplication with MountAsync "
                + "(which initializes the bridge in its mount path), or await BrowserRuntime.InitializeAsync() "
                + "before using the bare-renderer/selector primitives.");
        }
    }
}
