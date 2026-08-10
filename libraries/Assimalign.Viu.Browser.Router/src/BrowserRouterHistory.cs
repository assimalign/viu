using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// Creates Browser-owned web and hash histories and initializes their batched History API and
/// CSSOM View bridge.
/// </summary>
/// <remarks>
/// The Browser.Router leaf pairs this facade with host-free
/// <see cref="global::Assimalign.Viu.Router.RouterHistory.CreateMemory(string?)"/>. Web and hash
/// histories defer initialization until <see cref="global::Assimalign.Viu.Router.Router.ReadyAsync"/>.
/// These methods replace the former Router-owned <c>RouterHistory.CreateWeb</c>,
/// <c>RouterHistory.CreateWebHash</c>, and <c>RouterHistory.InitializeAsync</c> entry points so the
/// browser asset and generated interop remain in the Browser.Router leaf.
/// Specified by <c>[RTR-3]</c>, <c>[RTR-9]</c>, and <c>[RTR-10]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public static class BrowserRouterHistory
{
    private static Task? initialization;

    /// <summary>
    /// Loads the Browser.Router history module and binds its pop-state dispatch. Repeated calls
    /// await the first call's task and cancellation token.
    /// </summary>
    /// <param name="cancellationToken">Cancels the first module download.</param>
    /// <returns>The shared bridge-initialization task.</returns>
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>Creates a deferred clean-URL HTML History API integration.</summary>
    /// <param name="basePath">
    /// The base path, or <see langword="null"/> to use the document base element and then root.
    /// </param>
    /// <returns>The borrowed browser history supplied to a Router.</returns>
    public static IRouterHistory CreateWeb(string? basePath = null)
    {
        return new DeferredBrowserRouterHistory(
            isHash: false,
            basePath,
            InitializeAsync,
            static () => new JavaScriptBrowserHistoryInterop());
    }

    /// <summary>Creates a deferred hash-based HTML History API integration.</summary>
    /// <param name="basePath">
    /// The hash base, or <see langword="null"/> to derive it from the current document URL.
    /// </param>
    /// <returns>The borrowed browser history supplied to a Router.</returns>
    public static IRouterHistory CreateWebHash(string? basePath = null)
    {
        return new DeferredBrowserRouterHistory(
            isHash: true,
            basePath,
            InitializeAsync,
            static () => new JavaScriptBrowserHistoryInterop());
    }

    internal static string ResolveWebBase(
        IBrowserHistoryInterop interop,
        string? basePath)
    {
        string raw;
        if (!string.IsNullOrEmpty(basePath))
        {
            raw = basePath;
        }
        else
        {
            string? href = interop.ReadBaseHref();
            raw = href is null
                ? "/"
                : BrowserHistoryPathNormalization.StripBaseHrefOrigin(href);
        }

        return BrowserHistoryPathNormalization.NormalizeBase(raw);
    }

    internal static string ResolveHashBase(
        IBrowserHistoryInterop interop,
        string? basePath)
    {
        BrowserHistorySnapshot snapshot = interop.ReadSnapshot();
        string hashBase = BrowserHistoryPathNormalization.ComputeHashBase(
            basePath,
            snapshot.Host,
            snapshot.Pathname,
            snapshot.Search);
        return BrowserHistoryPathNormalization.NormalizeBase(hashBase);
    }

    private static async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await JSHost.ImportAsync(
            JavaScriptBrowserHistoryInterop.ModuleName,
            "/_content/Assimalign.Viu.Browser.Router/viu-history.js",
            cancellationToken).ConfigureAwait(false);
        await JavaScriptBrowserHistoryInterop.InitializeModuleAsync().ConfigureAwait(false);
    }
}
