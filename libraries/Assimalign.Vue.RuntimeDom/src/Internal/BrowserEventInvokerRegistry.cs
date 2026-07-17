using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The invoker pattern of <c>@vue/runtime-dom</c>'s events module
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/modules/events.ts):
/// exactly one DOM listener is attached per (element, event, options-signature), and a
/// re-render that changes the handler only swaps the invoker's .NET delegate — zero
/// <c>addEventListener</c>/<c>removeEventListener</c> interop calls. Prop-name suffixes parse
/// per Vue (<c>onClickOnce</c>, <c>onClickCapture</c>, <c>onClickPassive</c>, combined).
/// Bridge calls are injected delegates so the registry is unit-testable with no browser.
/// Handler exceptions route to <see cref="ErrorSink"/> instead of escaping into the JS
/// listener (the app-level pipeline plugs in with [V01.01.03.12]).
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal sealed class BrowserEventInvokerRegistry
{
    private readonly Dictionary<(int NodeHandle, string EventName, bool Capture), BrowserEventInvoker> _invokers = [];
    private readonly Action<int, string, bool, bool, bool> _addListener;
    private readonly Action<int, string, bool> _removeListener;

    internal BrowserEventInvokerRegistry(
        Action<int, string, bool, bool, bool> addListener,
        Action<int, string, bool> removeListener)
    {
        _addListener = addListener;
        _removeListener = removeListener;
    }

    /// <summary>
    /// The sink for handler exceptions. Defaults to a debug trace; the app error-handling
    /// pipeline ([V01.01.03.12]) replaces it. Never null.
    /// </summary>
    internal Action<Exception> ErrorSink { get; set; } = static exception =>
        Debug.WriteLine($"[Vue warn] Unhandled error in event handler: {exception}");

    /// <summary>The number of live invokers (diagnostics).</summary>
    internal int InvokerCount => _invokers.Count;

    /// <summary>
    /// Sets, swaps, or removes the handler for a raw event prop (upstream: <c>patchEvent</c>).
    /// </summary>
    /// <param name="nodeHandle">The element handle.</param>
    /// <param name="rawPropertyName">The full prop name (e.g. <c>"onClickCaptureOnce"</c>).</param>
    /// <param name="handler">The handler delegate, or null to remove.</param>
    internal void SetListener(int nodeHandle, string rawPropertyName, Delegate? handler)
    {
        var (eventName, once, capture, passive) = ParseEventName(rawPropertyName);
        var key = (nodeHandle, eventName, capture);
        if (handler is null)
        {
            if (_invokers.Remove(key))
            {
                _removeListener(nodeHandle, eventName, capture);
            }
            return;
        }
        if (_invokers.TryGetValue(key, out var invoker))
        {
            // The whole point of the pattern: a re-rendered handler is a delegate swap only.
            invoker.Handler = handler;
            return;
        }
        _invokers[key] = new BrowserEventInvoker { Handler = handler };
        _addListener(nodeHandle, eventName, once, capture, passive);
    }

    /// <summary>
    /// Routes one dispatched event to its invoker, returning the response flags
    /// (bit 0: stopPropagation, bit 1: preventDefault) the bridge applies to the live event.
    /// </summary>
    /// <param name="nodeHandle">The element handle the listener is attached to.</param>
    /// <param name="capture">Whether the capture-phase listener fired.</param>
    /// <param name="browserEvent">The marshaled event payload.</param>
    internal int Dispatch(int nodeHandle, bool capture, BrowserEvent browserEvent)
    {
        if (!_invokers.TryGetValue((nodeHandle, browserEvent.EventName, capture), out var invoker))
        {
            return 0;
        }
        try
        {
            // Reflection-free dispatch; a multicast delegate of either shape invokes every
            // merged target natively (MergeProperties chains same-typed handlers).
            switch (invoker.Handler)
            {
                case Action<BrowserEvent> typedHandler:
                    typedHandler(browserEvent);
                    break;
                case Action untypedHandler:
                    untypedHandler();
                    break;
                default:
                    throw new NotSupportedException(
                        $"Event handler for '{browserEvent.EventName}' is a "
                        + $"{invoker.Handler.GetType().Name}; handlers must be Action or Action<BrowserEvent>.");
            }
        }
        catch (Exception exception)
        {
            // Never escape into the JS listener; route to the error seam ([V01.01.03.12]).
            ErrorSink(exception);
        }
        return browserEvent.ToResponseFlags();
    }

    /// <summary>Drops the invokers of released node handles (bridge removal already detached DOM listeners).</summary>
    /// <param name="releasedHandles">The handles the bridge released.</param>
    internal void PurgeReleasedHandles(int[]? releasedHandles)
    {
        if (releasedHandles is null || releasedHandles.Length == 0 || _invokers.Count == 0)
        {
            return;
        }
        List<(int, string, bool)>? stale = null;
        foreach (var key in _invokers.Keys)
        {
            if (Array.IndexOf(releasedHandles, key.NodeHandle) >= 0)
            {
                (stale ??= []).Add(key);
            }
        }
        if (stale is not null)
        {
            foreach (var key in stale)
            {
                _invokers.Remove(key);
            }
        }
    }

    /// <summary>
    /// Parses a Vue event prop name (upstream: <c>parseName</c>): the trailing
    /// <c>Once</c>/<c>Capture</c>/<c>Passive</c> suffixes map to listener options, in any
    /// combination; the rest is the lower-cased event name.
    /// </summary>
    /// <param name="rawPropertyName">The full prop name (e.g. <c>"onClickOnce"</c>).</param>
    internal static (string EventName, bool Once, bool Capture, bool Passive) ParseEventName(string rawPropertyName)
    {
        var name = rawPropertyName.StartsWith("on", StringComparison.Ordinal)
            ? rawPropertyName[2..]
            : rawPropertyName;
        var once = false;
        var capture = false;
        var passive = false;
        while (true)
        {
            if (!once && name.EndsWith("Once", StringComparison.Ordinal))
            {
                name = name[..^4];
                once = true;
                continue;
            }
            if (!capture && name.EndsWith("Capture", StringComparison.Ordinal))
            {
                name = name[..^7];
                capture = true;
                continue;
            }
            if (!passive && name.EndsWith("Passive", StringComparison.Ordinal))
            {
                name = name[..^7];
                passive = true;
                continue;
            }
            break;
        }
        return (name.ToLowerInvariant(), once, capture, passive);
    }

    private sealed class BrowserEventInvoker
    {
        internal required Delegate Handler { get; set; }
    }
}
