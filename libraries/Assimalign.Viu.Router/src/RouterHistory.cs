using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// The factory facade for the router's history modes — the C# port of vue-router's
/// <c>createMemoryHistory</c>, <c>createWebHistory</c>, and <c>createWebHashHistory</c>
/// (<c>packages/router/src/history/</c>). Each returns an <see cref="IRouterHistory"/>; the memory
/// mode is pure and needs no browser, while the web and hash modes drive the History API over
/// interop and require <see cref="InitializeAsync"/> to have completed first.
/// </summary>
public static class RouterHistory
{
    private static Task? initialization;

    /// <summary>
    /// Loads this package's <c>viu-history.js</c> bridge module and binds the <c>popstate</c>
    /// dispatch. Idempotent — later calls await the same initialization. Must complete before
    /// <see cref="CreateWeb"/> or <see cref="CreateWebHash"/>. The memory mode does not need it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the module download.</param>
    [SupportedOSPlatform("browser")]
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>
    /// Creates an in-memory history (upstream <c>createMemoryHistory</c>) — pure, interop-free, and
    /// the mode used for tests and non-browser hosts.
    /// </summary>
    /// <param name="basePath">The base path, or <see langword="null"/> for none.</param>
    public static IRouterHistory CreateMemory(string? basePath = null)
        => new MemoryRouterHistory(basePath);

    /// <summary>
    /// Creates an HTML5 History API history (upstream <c>createWebHistory</c>): clean URLs driven by
    /// <c>pushState</c>/<c>replaceState</c> with a <c>popstate</c> listener. When no
    /// <paramref name="basePath"/> is given, the document <c>&lt;base&gt;</c> href (origin stripped)
    /// or <c>"/"</c> is used.
    /// </summary>
    /// <param name="basePath">The base path (e.g. <c>"/app/"</c>), or <see langword="null"/> to auto-detect.</param>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    [SupportedOSPlatform("browser")]
    public static IRouterHistory CreateWeb(string? basePath = null)
    {
        EnsureInitialized();
        var interop = new JavaScriptBrowserHistoryInterop();
        return new BrowserRouterHistory(interop, ResolveWebBase(interop, basePath));
    }

    /// <summary>
    /// Creates a hash-mode history (upstream <c>createWebHashHistory</c>): the whole route lives in
    /// <c>location.hash</c>, so navigation never triggers a server request. The base defaults from
    /// the current <c>location.pathname</c>/<c>search</c>, with a <c>#</c> ensured.
    /// </summary>
    /// <param name="basePath">The base (e.g. <c>"/folder/#/app/"</c>), or <see langword="null"/> to auto-detect.</param>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    [SupportedOSPlatform("browser")]
    public static IRouterHistory CreateWebHash(string? basePath = null)
    {
        EnsureInitialized();
        var interop = new JavaScriptBrowserHistoryInterop();
        return new BrowserRouterHistory(interop, ResolveHashBase(interop, basePath));
    }

    // Web base: a configured base wins; otherwise the <base> href (origin stripped) or "/".
    // Mirrors normalizeBase's <base>-element branch. Interop-agnostic, so unit-testable with a fake.
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

    [SupportedOSPlatform("browser")]
    private static void EnsureInitialized()
    {
        if (initialization is not { IsCompletedSuccessfully: true })
        {
            throw new InvalidOperationException(
                "RouterHistory.InitializeAsync() must complete before creating a web or hash history.");
        }
    }
}
