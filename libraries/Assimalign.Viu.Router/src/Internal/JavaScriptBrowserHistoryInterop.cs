using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// The real browser edge of the history: the <c>[JSImport]</c> bindings to this package's
/// <c>viu-history.js</c> module. It is a thin flattener — the DOM-free policy
/// (<see cref="BrowserRouterHistory"/>) computes every URL and <see cref="RouterHistoryState"/>, and
/// this class marshals them into flat, primitives-only interop crossings (no object-graph
/// marshaling): a null <c>back</c>/<c>forward</c> link and an absent scroll are encoded as an empty
/// string / <see langword="false"/> flag rather than a null. Mirrors the
/// <c>BrowserDomBridge</c>/<c>viu-dom.js</c> pattern of <c>Assimalign.Viu.RuntimeDom</c>.
/// Single-threaded by design — never call off the main WASM thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class JavaScriptBrowserHistoryInterop : IBrowserHistoryInterop
{
    /// <summary>The JS module name; doubles as the assembly key for <c>getAssemblyExports</c>.</summary>
    internal const string ModuleName = "Assimalign.Viu.Router";

    // 0 is the "not subscribed" sentinel (dispatch ids start at 1).
    private int subscriptionId;

    internal static Task InitializeModuleAsync()
        => Imports.Initialize();

    /// <inheritdoc/>
    public BrowserHistorySnapshot ReadSnapshot()
        => BrowserHistorySnapshotMarshaller.Decode(Imports.ReadSnapshot());

    /// <inheritdoc/>
    public string? ReadBaseHref()
    {
        var href = Imports.ReadBaseHref();
        return string.IsNullOrEmpty(href) ? null : href;
    }

    /// <inheritdoc/>
    public void Push(string currentUrl, RouterHistoryState amendedCurrentState, string toUrl, RouterHistoryState newState)
        => Imports.Push(
            currentUrl,
            amendedCurrentState.Back ?? string.Empty,
            amendedCurrentState.Current,
            amendedCurrentState.Forward ?? string.Empty,
            amendedCurrentState.Replaced,
            amendedCurrentState.Position,
            toUrl,
            newState.Back ?? string.Empty,
            newState.Current,
            newState.Forward ?? string.Empty,
            newState.Replaced,
            newState.Position,
            newState.Scroll.HasValue,
            newState.Scroll?.Left ?? 0d,
            newState.Scroll?.Top ?? 0d);

    /// <inheritdoc/>
    public void Replace(string toUrl, RouterHistoryState newState)
        => Imports.Replace(
            toUrl,
            newState.Back ?? string.Empty,
            newState.Current,
            newState.Forward ?? string.Empty,
            newState.Replaced,
            newState.Position,
            newState.Scroll.HasValue,
            newState.Scroll?.Left ?? 0d,
            newState.Scroll?.Top ?? 0d);

    /// <inheritdoc/>
    public void Go(int delta)
        => Imports.Go(delta);

    /// <inheritdoc/>
    public void Subscribe(Action<BrowserHistorySnapshot> onPopState)
    {
        subscriptionId = BrowserHistoryInteropDispatch.Register(onPopState);
        Imports.Subscribe(subscriptionId);
    }

    /// <inheritdoc/>
    public void Unsubscribe()
    {
        if (subscriptionId == 0)
        {
            return;
        }
        Imports.Unsubscribe(subscriptionId);
        BrowserHistoryInteropDispatch.Unregister(subscriptionId);
        subscriptionId = 0;
    }

    // --- raw imports (module: Assimalign.Viu.Router -> wwwroot/viu-history.js) --------------------

    private static partial class Imports
    {
        [JSImport("history.readSnapshot", ModuleName)]
        [return: JSMarshalAs<JSType.Array<JSType.String>>]
        internal static partial string[] ReadSnapshot();

        [JSImport("history.readBaseHref", ModuleName)]
        internal static partial string? ReadBaseHref();

        [JSImport("history.push", ModuleName)]
        internal static partial void Push(
            string currentUrl,
            string amendedBack,
            string amendedCurrent,
            string amendedForward,
            bool amendedReplaced,
            int amendedPosition,
            string toUrl,
            string newBack,
            string newCurrent,
            string newForward,
            bool newReplaced,
            int newPosition,
            bool newHasScroll,
            double newScrollLeft,
            double newScrollTop);

        [JSImport("history.replace", ModuleName)]
        internal static partial void Replace(
            string url,
            string back,
            string current,
            string forward,
            bool replaced,
            int position,
            bool hasScroll,
            double scrollLeft,
            double scrollTop);

        [JSImport("history.go", ModuleName)]
        internal static partial void Go(int delta);

        [JSImport("history.subscribe", ModuleName)]
        internal static partial void Subscribe(int subscriptionId);

        [JSImport("history.unsubscribe", ModuleName)]
        internal static partial void Unsubscribe(int subscriptionId);

        [JSImport("initialize", ModuleName)]
        [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
        internal static partial Task Initialize();
    }
}
