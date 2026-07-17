using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.RuntimeDom.Tests;

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
        bool isSelfTarget = true)
        => new(eventName, 100, key, string.Empty, modifiers, button, 0, 0, 0, 1, isSelfTarget, null, false);

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
}
