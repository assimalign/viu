using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Router;

/// <summary>
/// The single <c>[JSExport]</c> entry a browser <c>popstate</c> calls back into (the history
/// analogue of <c>BrowserEventDispatch</c>): the one JS listener per history instance forwards the
/// arrived entry's location and state as flat primitives in this one call, and the dispatch routes
/// it — by the subscription id issued at <see cref="Register"/> — to that instance's handler. Keying
/// on an id (rather than a single ambient handler) lets many histories coexist and lets
/// <see cref="Unregister"/> drop a torn-down one so no handler leaks across instances (test hosts
/// create many). Single-threaded by design — invoked only on the JS event loop.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserHistoryInteropDispatch
{
    private static readonly Dictionary<int, Action<BrowserHistorySnapshot>> Handlers = [];
    private static int nextSubscriptionId = 1;

    /// <summary>Registers a popstate handler and returns its subscription id.</summary>
    /// <param name="handler">The policy's popstate handler.</param>
    internal static int Register(Action<BrowserHistorySnapshot> handler)
    {
        var id = nextSubscriptionId++;
        Handlers[id] = handler;
        return id;
    }

    /// <summary>Drops a subscription so its handler is never invoked again.</summary>
    /// <param name="subscriptionId">The id returned by <see cref="Register"/>.</param>
    internal static void Unregister(int subscriptionId)
        => Handlers.Remove(subscriptionId);

    /// <summary>
    /// The <c>[JSExport]</c> the JS <c>popstate</c> listener invokes with the arrived entry's raw
    /// location components and state as primitives (no <c>JSObject</c> reads, no per-property calls).
    /// </summary>
    [JSExport]
    internal static void DispatchPopState(
        int subscriptionId,
        string pathname,
        string search,
        string hash,
        string host,
        int historyLength,
        bool hasState,
        string? back,
        string current,
        string? forward,
        bool replaced,
        int position,
        bool hasScroll,
        double scrollLeft,
        double scrollTop)
    {
        if (!Handlers.TryGetValue(subscriptionId, out var handler))
        {
            return;
        }
        var state = hasState
            ? new RouterHistoryState(
                Back: back,
                Current: current,
                Forward: forward,
                Replaced: replaced,
                Position: position,
                Scroll: hasScroll ? new ScrollPosition(scrollLeft, scrollTop) : null)
            : null;
        handler(new BrowserHistorySnapshot(pathname, search, hash, host, historyLength, state));
    }
}
