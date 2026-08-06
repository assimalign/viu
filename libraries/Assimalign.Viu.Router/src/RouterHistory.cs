using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// The factory facade for the router's three history modes. Each returns an
/// <see cref="IRouterHistory"/>; the memory mode is pure and needs no browser, while the web and hash
/// modes defer their History API bridge initialization until <see cref="Router.ReadyAsync"/>.
/// Specified by <c>[RTR-3]</c>.
/// </summary>
public static class RouterHistory
{
    private static Task? initialization;

    /// <summary>
    /// Loads this package's <c>viu-history.js</c> bridge module and binds the <c>popstate</c>
    /// dispatch. Idempotent — later calls await the same initialization. Web and hash histories call
    /// this lazily from <see cref="Router.ReadyAsync"/>; applications may call it earlier only to
    /// prewarm the bridge. The memory mode does not need it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    /// <returns>
    /// The cached bridge-initialization task. The first call's cancellation token owns that task;
    /// later calls cannot replace it.
    /// </returns>
    [SupportedOSPlatform("browser")]
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>
    /// Creates an in-memory history — pure, interop-free, and
    /// the mode used for tests and non-browser hosts.
    /// </summary>
    /// <param name="basePath">The base path, or <see langword="null"/> for none.</param>
    public static IRouterHistory CreateMemory(string? basePath = null)
        => new MemoryRouterHistory(basePath);

    /// <summary>
    /// Creates an HTML5 History API history: clean URLs driven by
    /// <c>pushState</c>/<c>replaceState</c> with a <c>popstate</c> listener. When no
    /// <paramref name="basePath"/> is given, the document <c>&lt;base&gt;</c> href (origin stripped)
    /// or <c>"/"</c> is used.
    /// </summary>
    /// <param name="basePath">The base path (e.g. <c>"/app/"</c>), or <see langword="null"/> to auto-detect.</param>
    /// <returns>
    /// A deferred browser history. A router may attach its listener immediately; awaiting
    /// <see cref="Router.ReadyAsync"/> initializes every other synchronous member.
    /// </returns>
    [SupportedOSPlatform("browser")]
    public static IRouterHistory CreateWeb(string? basePath = null)
    {
        return new DeferredBrowserRouterHistory(
            isHash: false,
            basePath,
            InitializeAsync,
            static () => new JavaScriptBrowserHistoryInterop());
    }

    /// <summary>
    /// Creates a hash-mode history: the whole route lives in
    /// <c>location.hash</c>, so navigation never triggers a server request. The base defaults from
    /// the current <c>location.pathname</c>/<c>search</c>, with a <c>#</c> ensured.
    /// </summary>
    /// <param name="basePath">The base (e.g. <c>"/folder/#/app/"</c>), or <see langword="null"/> to auto-detect.</param>
    /// <returns>
    /// A deferred browser history. A router may attach its listener immediately; awaiting
    /// <see cref="Router.ReadyAsync"/> initializes every other synchronous member.
    /// </returns>
    [SupportedOSPlatform("browser")]
    public static IRouterHistory CreateWebHash(string? basePath = null)
    {
        return new DeferredBrowserRouterHistory(
            isHash: true,
            basePath,
            InitializeAsync,
            static () => new JavaScriptBrowserHistoryInterop());
    }

    // Web base: a configured base wins; otherwise the <base> href (origin stripped) or "/".
    // The <base>-element branch of base resolution. Interop-agnostic, so unit-testable with a fake.
    internal static string ResolveWebBase(IBrowserHistoryInterop interop, string? basePath)
    {
        string raw;
        if (!string.IsNullOrEmpty(basePath))
        {
            raw = basePath;
        }
        else
        {
            var href = interop.ReadBaseHref();
            raw = href is null ? "/" : HistoryPathNormalization.StripBaseHrefOrigin(href);
        }
        return HistoryPathNormalization.NormalizeBase(raw);
    }

    // Hash base: computed from the current URL then normalized, exactly as createWebHashHistory
    // forwards its computed base to createWebHistory. Interop-agnostic, so unit-testable with a fake.
    internal static string ResolveHashBase(IBrowserHistoryInterop interop, string? basePath)
    {
        var snapshot = interop.ReadSnapshot();
        var hashBase = HistoryPathNormalization.ComputeHashBase(
            basePath, snapshot.Host, snapshot.Pathname, snapshot.Search);
        return HistoryPathNormalization.NormalizeBase(hashBase);
    }

    [SupportedOSPlatform("browser")]
    private static async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        // The module ships with this package as a static web asset; consumers get it under
        // /_content/<package id>/ (the module name doubles as the [JSImport] module key).
        await JSHost.ImportAsync(
            JavaScriptBrowserHistoryInterop.ModuleName,
            "/_content/Assimalign.Viu.Router/viu-history.js",
            cancellationToken);
        await JavaScriptBrowserHistoryInterop.InitializeModuleAsync();
    }
}
