using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// The real browser edge of the history: the <c>[JSImport]</c> bindings to this package's
/// <c>viu-history.js</c> module. It is a thin flattener — the DOM-free policy
/// (<see cref="BrowserRouterHistoryImplementation"/>) computes every URL and <see cref="RouterHistoryState"/>, and
/// this class marshals them into flat, primitives-only interop crossings (no object-graph
/// marshaling): a null <c>back</c>/<c>forward</c> link and an absent scroll are encoded as an empty
/// string / <see langword="false"/> flag rather than a null. It follows the same
/// <c>BrowserDomBridge</c>/<c>viu-dom.js</c> pattern <c>Assimalign.Viu.Browser</c> uses, so every
/// JS module in the framework is loaded and torn down the same way.
/// Single-threaded by design — never call off the main WASM thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class JavaScriptBrowserHistoryInterop : IBrowserHistoryInterop
{
    /// <summary>The JS module name; doubles as the assembly key for <c>getAssemblyExports</c>.</summary>
    internal const string ModuleName = "Assimalign.Viu.Browser.Router";

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
    public void Push(
        int subscriptionIdentifier,
        string currentUrl,
        RouterHistoryState amendedCurrentState,
        string toUrl,
        RouterHistoryState newState)
        => Imports.Push(
            subscriptionIdentifier,
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
    public void Replace(
        int subscriptionIdentifier,
        string toUrl,
        RouterHistoryState newState)
        => Imports.Replace(
            subscriptionIdentifier,
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
    public void Scroll(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ScrollPosition? position = target.Position;
        Imports.Scroll(
            target.Selector ?? string.Empty,
            position.HasValue,
            position?.Left ?? 0d,
            position?.Top ?? 0d,
            target.Offset.Left,
            target.Offset.Top,
            target.Smooth);
    }

    /// <inheritdoc/>
    public int Subscribe(Action<BrowserHistorySnapshot> onPopState)
    {
        int subscriptionIdentifier = BrowserHistoryInteropDispatch.Register(onPopState);
        try
        {
            Imports.Subscribe(subscriptionIdentifier);
            return subscriptionIdentifier;
        }
        catch
        {
            BrowserHistoryInteropDispatch.Unregister(subscriptionIdentifier);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Unsubscribe(int subscriptionIdentifier)
    {
        try
        {
            Imports.Unsubscribe(subscriptionIdentifier);
        }
        finally
        {
            BrowserHistoryInteropDispatch.Unregister(subscriptionIdentifier);
        }
    }

    // --- raw imports (module: Assimalign.Viu.Browser.Router -> wwwroot/viu-history.js) ------------

    private static partial class Imports
    {
        [JSImport("history.readSnapshot", ModuleName)]
        [return: JSMarshalAs<JSType.Array<JSType.String>>]
        internal static partial string[] ReadSnapshot();

        [JSImport("history.readBaseHref", ModuleName)]
        internal static partial string? ReadBaseHref();

        [JSImport("history.push", ModuleName)]
        internal static partial void Push(
            int subscriptionIdentifier,
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
            int subscriptionIdentifier,
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

        [JSImport("history.scroll", ModuleName)]
        internal static partial void Scroll(
            string selector,
            bool hasPosition,
            double left,
            double top,
            double offsetLeft,
            double offsetTop,
            bool smooth);

        [JSImport("history.subscribe", ModuleName)]
        internal static partial void Subscribe(int subscriptionIdentifier);

        [JSImport("history.unsubscribe", ModuleName)]
        internal static partial void Unsubscribe(int subscriptionIdentifier);

        [JSImport("initialize", ModuleName)]
        [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
        internal static partial Task Initialize();
    }
}
