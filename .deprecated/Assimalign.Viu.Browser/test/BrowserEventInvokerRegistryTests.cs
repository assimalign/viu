using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// Pins the invoker pattern of @vue/runtime-dom's events module
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/modules/events.ts):
// handler updates between renders are delegate swaps with ZERO listener-management interop,
// asserted through an instrumented bridge (recorded add/remove delegates).
public class BrowserEventInvokerRegistryTests
{
    private const int Element = 9;

    private readonly List<string> _bridgeCalls = [];
    private readonly BrowserEventInvokerRegistry _registry;

    public BrowserEventInvokerRegistryTests()
    {
        _registry = new BrowserEventInvokerRegistry(
            (handle, eventName, once, capture, passive) =>
                _bridgeCalls.Add($"add({handle},{eventName},once:{once},capture:{capture},passive:{passive})"),
            (handle, eventName, capture) =>
                _bridgeCalls.Add($"remove({handle},{eventName},capture:{capture})"));
    }

    private static BrowserEvent Event(
        string eventName = "click",
        string key = "",
        BrowserEventModifiers modifiers = BrowserEventModifiers.None,
        int button = 0,
        bool isSelfTarget = true,
        bool defaultPrevented = false)
        => new(eventName, 100, key, string.Empty, modifiers, button, 0, 0, 0, 1, isSelfTarget, null, false, null, defaultPrevented);

    [Fact]
    public void SwappingTheHandlerBetweenRenders_MakesZeroBridgeCalls()
    {
        var invoked = new List<string>();
        _registry.SetListener(Element, "onClick", (Action)(() => invoked.Add("first")));
        _bridgeCalls.Count.ShouldBe(1); // the one and only addEventListener

        _registry.SetListener(Element, "onClick", (Action)(() => invoked.Add("second")));
        _registry.SetListener(Element, "onClick", (Action)(() => invoked.Add("third")));

        // The whole point of the invoker pattern: swaps cost nothing at the boundary.
        _bridgeCalls.Count.ShouldBe(1);

        _registry.Dispatch(Element, capture: false, Event());
        invoked.ShouldBe(["third"]); // the swapped-in handler runs
    }

    [Fact]
    public void RemovingTheHandler_RemovesTheListenerOnce()
    {
        _registry.SetListener(Element, "onClick", (Action)(() => { }));
        _registry.SetListener(Element, "onClick", null);

        _bridgeCalls.ShouldBe(
        [
            $"add({Element},click,once:False,capture:False,passive:False)",
            $"remove({Element},click,capture:False)",
        ]);
        _registry.InvokerCount.ShouldBe(0);
    }

    [Fact]
    public void SuffixParsing_MapsToListenerOptions()
    {
        // Upstream parseName: onClickOnce / onClickCapture / onClickPassive and combinations.
        BrowserEventInvokerRegistry.ParseEventName("onClick").ShouldBe(("click", false, false, false));
        BrowserEventInvokerRegistry.ParseEventName("onClickOnce").ShouldBe(("click", true, false, false));
        BrowserEventInvokerRegistry.ParseEventName("onClickCapture").ShouldBe(("click", false, true, false));
        BrowserEventInvokerRegistry.ParseEventName("onClickPassive").ShouldBe(("click", false, false, true));
        BrowserEventInvokerRegistry.ParseEventName("onClickCaptureOnce").ShouldBe(("click", true, true, false));
        BrowserEventInvokerRegistry.ParseEventName("onScrollPassiveCapture").ShouldBe(("scroll", false, true, true));
        BrowserEventInvokerRegistry.ParseEventName("onKeydown").ShouldBe(("keydown", false, false, false));
    }

    [Fact]
    public void SuffixedListeners_AttachWithTheCorrespondingOptions()
    {
        _registry.SetListener(Element, "onClickCaptureOnce", (Action)(() => { }));
        _bridgeCalls.ShouldBe([$"add({Element},click,once:True,capture:True,passive:False)"]);
    }

    [Fact]
    public void CaptureAndBubbleListeners_AreIndependentInvokers()
    {
        var invoked = new List<string>();
        _registry.SetListener(Element, "onClick", (Action)(() => invoked.Add("bubble")));
        _registry.SetListener(Element, "onClickCapture", (Action)(() => invoked.Add("capture")));

        _bridgeCalls.Count.ShouldBe(2);
        _registry.Dispatch(Element, capture: true, Event());
        _registry.Dispatch(Element, capture: false, Event());
        invoked.ShouldBe(["capture", "bubble"]);
    }

    [Fact]
    public void Dispatch_SupportsTypedHandlers_AndReturnsResponseFlags()
    {
        string? seenKey = null;
        _registry.SetListener(Element, "onKeydown", (Action<BrowserEvent>)(browserEvent =>
        {
            seenKey = browserEvent.Key;
            browserEvent.StopPropagation();
            browserEvent.PreventDefault();
        }));

        var flags = _registry.Dispatch(Element, capture: false, Event("keydown", key: "Enter"));

        seenKey.ShouldBe("Enter");
        flags.ShouldBe(3); // bit 0 stopPropagation, bit 1 preventDefault
    }

    [Fact]
    public void Dispatch_WithNoInvoker_ReturnsZero()
        => _registry.Dispatch(Element, capture: false, Event()).ShouldBe(0);

    // --- renderer-agnostic object-payload handlers ([V01.01.08.03.01]: RouterLink and other
    // components rendering through the node-ops abstraction attach an Action<object?> whose payload a
    // host bridge synthesizes from the BrowserEvent; installed via BrowserObjectEvents.Invoker) ------

    [Fact]
    public void Dispatch_ObjectPayloadHandler_RoutesThroughTheInstalledInvoker()
    {
        object? seenPayload = null;
        var payload = new object();
        var previous = BrowserObjectEvents.Invoker;
        try
        {
            BrowserObjectEvents.Invoker = (handler, browserEvent) =>
            {
                handler(payload);              // the bridge feeds a host-synthesized payload
                browserEvent.PreventDefault(); // ...and applies the handler's decision back
            };
            _registry.SetListener(Element, "onClick", (Action<object?>)(value => seenPayload = value));

            var flags = _registry.Dispatch(Element, capture: false, Event());

            seenPayload.ShouldBeSameAs(payload);
            flags.ShouldBe(2); // the invoker's PreventDefault re-crosses the boundary
        }
        finally
        {
            BrowserObjectEvents.Invoker = previous;
        }
    }

    [Fact]
    public void Dispatch_ObjectPayloadHandler_WithNoInvokerInstalled_RoutesToTheErrorSink()
    {
        Exception? sunk = null;
        _registry.ErrorSink = exception => sunk = exception;
        var previous = BrowserObjectEvents.Invoker;
        try
        {
            BrowserObjectEvents.Invoker = null;
            _registry.SetListener(Element, "onClick", (Action<object?>)(_ => { }));

            var flags = -1;
            Should.NotThrow(() => flags = _registry.Dispatch(Element, capture: false, Event()));

            sunk.ShouldBeOfType<NotSupportedException>();
            flags.ShouldBe(0); // no handler ran, so nothing re-crosses the boundary
        }
        finally
        {
            BrowserObjectEvents.Invoker = previous;
        }
    }

    [Fact]
    public void Dispatch_EventArrivingPrevented_ReportsDefaultPreventedWithoutResignaling()
    {
        // An event that arrived already prevented reads defaultPrevented (upstream guardEvent bails on
        // it) but does not re-signal preventDefault — the browser already applied it.
        bool? seenPrevented = null;
        _registry.SetListener(Element, "onClick", (Action<BrowserEvent>)(browserEvent =>
            seenPrevented = browserEvent.DefaultPrevented));

        var flags = _registry.Dispatch(Element, capture: false, Event(defaultPrevented: true));

        seenPrevented.ShouldBe(true);
        flags.ShouldBe(0); // arrival-prevented alone re-crosses nothing
    }

    [Fact]
    public void HandlerExceptions_RouteToTheErrorSink_NeverEscapeToTheListener()
    {
        Exception? sunk = null;
        _registry.ErrorSink = exception => sunk = exception;
        _registry.SetListener(Element, "onClick", (Action)(() => throw new InvalidOperationException("handler boom")));

        var flags = 0;
        Should.NotThrow(() => flags = _registry.Dispatch(Element, capture: false, Event()));

        sunk.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("handler boom");
        flags.ShouldBe(0);
    }

    [Fact]
    public void PurgeReleasedHandles_DropsInvokersForRemovedNodes()
    {
        _registry.SetListener(Element, "onClick", (Action)(() => { }));
        _registry.SetListener(31, "onInput", (Action)(() => { }));

        _registry.PurgeReleasedHandles([Element]);

        _registry.InvokerCount.ShouldBe(1);
        _registry.Dispatch(Element, capture: false, Event()).ShouldBe(0);
    }

    [Fact]
    public void MulticastHandlers_InvokeEveryTargetInOrder()
    {
        // MergeProperties chains same-typed handlers into one multicast delegate.
        var invoked = new List<string>();
        var merged = Delegate.Combine(
            (Action<BrowserEvent>)(_ => invoked.Add("first")),
            (Action<BrowserEvent>)(browserEvent => invoked.Add("second:" + browserEvent.EventName)));
        _registry.SetListener(Element, "onClick", merged);

        _registry.Dispatch(Element, capture: false, Event());

        invoked.ShouldBe(["first", "second:click"]);
    }

    // --- model channel ([V01.01.04.06]: v-model listeners coexist with template @event props;
    // upstream attaches the directive's listener with a separate raw addEventListener) ----------

    [Fact]
    public void ModelAndPropertyChannels_ShareOneDomListener_AndBothFire()
    {
        var invoked = new List<string>();
        _registry.SetListener(Element, "onInput", (Action)(() => invoked.Add("property")));
        _registry.SetModelListener(Element, "onInput", (Action)(() => invoked.Add("model")));

        _bridgeCalls.Count.ShouldBe(1); // one addEventListener for both channels

        _registry.Dispatch(Element, capture: false, Event("input"));
        invoked.ShouldBe(["property", "model"]); // property fires first, then the directive
    }

    [Fact]
    public void RemovingThePropertyChannel_KeepsTheModelChannelListening()
    {
        var invoked = new List<string>();
        _registry.SetModelListener(Element, "onInput", (Action)(() => invoked.Add("model")));
        _registry.SetListener(Element, "onInput", (Action)(() => invoked.Add("property")));

        _registry.SetListener(Element, "onInput", null); // template handler removed on re-render

        _bridgeCalls.ShouldBe([$"add({Element},input,once:False,capture:False,passive:False)"]); // no remove
        _registry.Dispatch(Element, capture: false, Event("input"));
        invoked.ShouldBe(["model"]);
    }

    [Fact]
    public void RemovingBothChannels_RemovesTheDomListenerOnce()
    {
        _registry.SetModelListener(Element, "onInput", (Action)(() => { }));
        _registry.SetListener(Element, "onInput", (Action)(() => { }));

        _registry.SetListener(Element, "onInput", null);
        _registry.SetModelListener(Element, "onInput", null);

        _bridgeCalls.ShouldBe(
        [
            $"add({Element},input,once:False,capture:False,passive:False)",
            $"remove({Element},input,capture:False)",
        ]);
        _registry.InvokerCount.ShouldBe(0);
    }

    [Fact]
    public void SwappingTheModelHandler_MakesZeroBridgeCalls()
    {
        var invoked = new List<string>();
        _registry.SetModelListener(Element, "onChange", (Action)(() => invoked.Add("first")));
        _registry.SetModelListener(Element, "onChange", (Action)(() => invoked.Add("second")));

        _bridgeCalls.Count.ShouldBe(1); // re-rendered directive handler is a delegate swap only

        _registry.Dispatch(Element, capture: false, Event("change"));
        invoked.ShouldBe(["second"]);
    }
}
