using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Testing.Tests;

// Pins the @vue/runtime-test parity surface: op recording, serialize, and triggerEvent —
// https://github.com/vuejs/core/tree/main/packages/runtime-test.
public class TestRendererTests
{
    private readonly TestRenderer _renderer = new();

    [Fact]
    public void CompleteTree_MountsUpdatesAndUnmounts_WithNoBrowserInvolvement()
    {
        var container = _renderer.CreateContainer();

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("id", "app")),
                VirtualNodeFactory.Element("span", "one"),
                VirtualNodeFactory.Comment("marker")),
            container);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div id=\"app\"><span>one</span><!--marker--></div></root>");

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("id", "app")),
                VirtualNodeFactory.Element("span", "two"),
                VirtualNodeFactory.Comment("marker")),
            container);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div id=\"app\"><span>two</span><!--marker--></div></root>");

        _renderer.Render(null, container);
        TestNodeSerializer.Serialize(container).ShouldBe("<root></root>");
    }

    [Fact]
    public void OperationLog_RecordsTypeTargetAndArguments_AndResets()
    {
        var container = _renderer.CreateContainer();
        _renderer.Render(VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("id", "x")), "text"), container);

        var operations = _renderer.OperationLog.Operations;
        operations.ShouldNotBeEmpty();

        var create = _renderer.OperationLog.OfType(TestNodeOperationType.CreateElement)[0];
        create.TargetNode.ShouldBeOfType<TestElement>().Tag.ShouldBe("div");
        create.Text.ShouldBe("div");

        var patch = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty)[0];
        patch.PropertyName.ShouldBe("id");
        patch.PreviousValue.ShouldBeNull();
        patch.NextValue.ShouldBe("x");

        var insert = _renderer.OperationLog.OfType(TestNodeOperationType.Insert)[0];
        insert.ParentNode.ShouldBeSameAs(container);
        insert.AnchorNode.ShouldBeNull();

        _renderer.OperationLog.Reset();
        _renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void TargetedTextPatch_IsObservableInTheOpLog()
    {
        // The [V01.01.11.01] patch-efficiency criterion: a text-only compiled update produces
        // exactly one set-text op and zero structural ops.
        var container = _renderer.CreateContainer();
        VirtualNode Compiled(string text) => VirtualNodeFactory.Element("div", null, text, PatchFlags.Text);

        _renderer.Render(Compiled("a"), container);
        _renderer.OperationLog.Reset();
        _renderer.Render(Compiled("b"), container);

        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        _renderer.OperationLog.Operations.Count.ShouldBe(1);
    }

    [Fact]
    public void Serializer_OmitsListenersAndNulls_AndSupportsIndentation()
    {
        var container = _renderer.CreateContainer();
        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("id", "a"), ("onClick", (Action)(() => { })), ("hidden", null)),
                VirtualNodeFactory.Element("span", "inner")),
            container);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div id=\"a\"><span>inner</span></div></root>");

        var indented = TestNodeSerializer.Serialize(container, indent: 2);
        indented.ShouldContain("\n  <div");
        indented.ShouldContain("\n    <span");
    }

    [Fact]
    public void TriggerEvent_InvokesTheRegisteredListenerSynchronously()
    {
        var container = _renderer.CreateContainer();
        var clicks = 0;
        _renderer.Render(
            VirtualNodeFactory.Element(
                "button",
                VirtualNodeFactory.Properties(("onClick", (Action)(() => clicks++))),
                "press"),
            container);
        var button = (TestElement)container.Children[0];

        TestEventDispatcher.Trigger(button, "click").ShouldBeTrue();
        clicks.ShouldBe(1);
    }

    [Fact]
    public void TriggerEvent_PassesPayloadsAndInvokesMulticastListenersInOrder()
    {
        var container = _renderer.CreateContainer();
        var received = new List<object?>();
        var merged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("onInput", (Action<object?>)(payload => received.Add(payload)))),
            VirtualNodeFactory.Properties(("onInput", (Action<object?>)(payload => received.Add("second:" + payload)))));
        _renderer.Render(
            VirtualNodeFactory.Element("input", merged, (VirtualNode?[]?)null),
            container);
        var input = (TestElement)container.Children[0];

        TestEventDispatcher.Trigger(input, "input", "hello").ShouldBeTrue();

        received.ShouldBe(["hello", "second:hello"]);
    }

    [Fact]
    public void TriggerEvent_AfterListenerRemoval_ReturnsFalse()
    {
        var container = _renderer.CreateContainer();
        VirtualNode WithHandler(Action? handler) => VirtualNodeFactory.Element(
            "button", VirtualNodeFactory.Properties(("onClick", handler)), "press");

        var clicks = 0;
        _renderer.Render(WithHandler(() => clicks++), container);
        _renderer.Render(WithHandler(null), container);
        var button = (TestElement)container.Children[0];

        TestEventDispatcher.Trigger(button, "click").ShouldBeFalse();
        clicks.ShouldBe(0);
    }

    [Fact]
    public void TriggerEvent_OnAnUnknownEvent_ReturnsFalse()
    {
        var container = _renderer.CreateContainer();
        _renderer.Render(VirtualNodeFactory.Element("div", "x"), container);

        TestEventDispatcher.Trigger((TestElement)container.Children[0], "click").ShouldBeFalse();
    }

    [Fact]
    public void SchedulerPump_CapturesScheduledFlushes_UntilPumped()
    {
        using var pump = TestSchedulerPump.Install();
        var ran = false;

        Scheduler.QueueJob(new SchedulerJob(() => ran = true));

        ran.ShouldBeFalse(); // captured, not run
        pump.PendingFlushCount.ShouldBe(1);
        pump.RunUntilIdle().ShouldBe(1);
        ran.ShouldBeTrue();
        pump.PendingFlushCount.ShouldBe(0);
    }
}
