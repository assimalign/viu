using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assimalign.Viu.Browser;

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
/// <para>
/// Each invoker carries two independent handler channels for the same DOM event: the
/// <em>property</em> channel (a template <c>@event</c> / <c>onX</c> prop) and the <em>model</em>
/// channel (a <c>v-model</c> directive listener, [V01.01.04.06]). Upstream adds the directive's
/// listener with a separate raw <c>addEventListener</c>; a single shared DOM listener plus two
/// .NET channels reproduces that coexistence without a second interop listener — both fire, in
/// property-then-model order.
/// </para>
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
    /// Sets, swaps, or removes the property-channel handler for a raw event prop (upstream:
    /// <c>patchEvent</c>).
    /// </summary>
    /// <param name="nodeHandle">The element handle.</param>
    /// <param name="rawPropertyName">The full prop name (e.g. <c>"onClickCaptureOnce"</c>).</param>
    /// <param name="handler">The handler delegate, or null to remove.</param>
    internal void SetListener(int nodeHandle, string rawPropertyName, Delegate? handler)
        => SetChannel(nodeHandle, rawPropertyName, handler, model: false);

    /// <summary>
    /// Sets, swaps, or removes the model-channel handler for a raw event prop — the listener a
    /// <c>v-model</c> directive attaches ([V01.01.04.06]). Independent of the property channel, so
    /// a directive listener and a template <c>@event</c> handler on the same element coexist
    /// (upstream: v-model uses a separate raw listener).
    /// </summary>
    /// <param name="nodeHandle">The element handle.</param>
    /// <param name="rawPropertyName">The full prop name (e.g. <c>"onInput"</c>, <c>"onChange"</c>).</param>
    /// <param name="handler">The handler delegate, or null to remove.</param>
    internal void SetModelListener(int nodeHandle, string rawPropertyName, Delegate? handler)
        => SetChannel(nodeHandle, rawPropertyName, handler, model: true);

    private void SetChannel(int nodeHandle, string rawPropertyName, Delegate? handler, bool model)
    {
        var (eventName, once, capture, passive) = ParseEventName(rawPropertyName);
        var key = (nodeHandle, eventName, capture);
        if (_invokers.TryGetValue(key, out var invoker))
        {
            // The whole point of the pattern: a re-rendered handler is a delegate swap only.
            if (model)
            {
                invoker.ModelHandler = handler;
            }
            else
            {
                invoker.PropertyHandler = handler;
            }
            // Once both channels are empty the shared DOM listener has no reason to exist.
            if (invoker.PropertyHandler is null && invoker.ModelHandler is null)
            {
                _invokers.Remove(key);
                _removeListener(nodeHandle, eventName, capture);
            }
            return;
        }
        if (handler is null)
        {
            return;
        }
        var created = new BrowserEventInvoker();
        if (model)
        {
            created.ModelHandler = handler;
        }
        else
        {
            created.PropertyHandler = handler;
        }
        _invokers[key] = created;
        _addListener(nodeHandle, eventName, once, capture, passive);
    }

    /// <summary>
    /// Routes one dispatched event to its invoker, returning the response flags
    /// (bit 0: stopPropagation, bit 1: preventDefault) the bridge applies to the live event.
    /// Both channels fire in property-then-model order.
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
        Invoke(invoker.PropertyHandler, browserEvent);
        Invoke(invoker.ModelHandler, browserEvent);
        return browserEvent.ToResponseFlags();
    }

    private void Invoke(Delegate? handler, BrowserEvent browserEvent)
    {
        if (handler is null)
        {
            return;
        }
        try
        {
            // Reflection-free dispatch; a multicast delegate of either shape invokes every
            // merged target natively (MergeProperties chains same-typed handlers). The
            // Action<object?> case MUST precede Action<BrowserEvent>: Action<T> is contravariant, so
            // Action<object?> is a *subtype* of Action<BrowserEvent> (an object-taking handler can
            // stand in for a BrowserEvent-taking one) — ordering it after would make it unreachable
            // and silently feed RouterLink the BrowserEvent instead of its RouterLinkClickEvent.
            switch (handler)
            {
                case Action<object?> objectHandler:
                    // A renderer-agnostic handler (e.g. RouterLink's onClick) expects a host-synthesized
                    // payload, not the BrowserEvent. The installed bridge builds that payload, invokes
                    // the handler, and applies its prevent/stop decision back to browserEvent (whose
                    // response flags re-cross the boundary). With no bridge installed the handler cannot
                    // be serviced, so surface it — upstream has no equivalent, since JS handlers always
                    // receive the DOM event.
                    if (BrowserObjectEvents.Invoker is { } objectInvoker)
                    {
                        objectInvoker(objectHandler, browserEvent);
                    }
                    else
                    {
                        throw new NotSupportedException(
                            $"Event handler for '{browserEvent.EventName}' is an Action<object?> but no "
                            + $"{nameof(BrowserObjectEvents)}.{nameof(BrowserObjectEvents.Invoker)} is "
                            + "installed; install the Router DOM bridge to dispatch renderer-agnostic "
                            + "component events.");
                    }
                    break;
                case Action<BrowserEvent> typedHandler:
                    typedHandler(browserEvent);
                    break;
                case Action untypedHandler:
                    untypedHandler();
                    break;
                default:
                    throw new NotSupportedException(
                        $"Event handler for '{browserEvent.EventName}' is a "
                        + $"{handler.GetType().Name}; handlers must be Action, Action<BrowserEvent>, "
                        + "or Action<object?>.");
            }
        }
        catch (Exception exception)
        {
            // Never escape into the JS listener; route to the error seam ([V01.01.03.12]).
            ErrorSink(exception);
        }
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
        /// <summary>The template <c>@event</c> / <c>onX</c> prop handler, or null.</summary>
        internal Delegate? PropertyHandler { get; set; }

        /// <summary>The <c>v-model</c> directive handler, or null.</summary>
        internal Delegate? ModelHandler { get; set; }
    }
}
