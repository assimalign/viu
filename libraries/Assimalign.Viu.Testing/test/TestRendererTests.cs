using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestRendererTests
{
    private readonly TestRenderer _renderer = new();

    [Fact]
    public void CompleteTree_MountsPatchesAndUnmountsWithoutBrowser()
    {
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "div",
                Attributes(("id", "app")),
                [
                    ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text("one")]),
                    ComponentTree.Comment("marker"),
                ]),
            container);

        TestNodeSerializer.Serialize(container.Children[0])
            .ShouldBe("<div id=\"app\"><span>one</span><!--marker--></div>");

        _renderer.OperationLog.Reset();
        _renderer.Render(
            ComponentTree.Element(
                "div",
                Attributes(("id", "app")),
                [
                    ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text("two")]),
                    ComponentTree.Comment("marker"),
                ]),
            container);

        TestNodeSerializer.Serialize(container.Children[0])
            .ShouldBe("<div id=\"app\"><span>two</span><!--marker--></div>");
        _renderer.OperationLog.Count(TestNodeOperationType.SetText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);

        _renderer.Render(null, container);
        container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void OperationLog_RecordsPrimitiveHostBoundaryCalls()
    {
        TestElement container = _renderer.CreateContainer();

        _renderer.Render(
            ComponentTree.Element(
                "div",
                Attributes(("id", "x")),
                [ComponentTree.Text("text")]),
            container);

        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateText).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.PatchAttribute).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(2);
    }

    [Fact]
    public void CompiledTextFlag_PatchesOnlyTheTextHostNode()
    {
        TestElement container = _renderer.CreateContainer();
        IElementComponent Compiled(string text)
        {
            return ComponentTree.Element(
                "div",
                children: [ComponentTree.Text(text)],
                optimization: new ComponentOptimization(PatchFlags.Text));
        }

        _renderer.Render(Compiled("a"), container);
        _renderer.OperationLog.Reset();
        _renderer.Render(Compiled("b"), container);

        _renderer.OperationLog.Count(TestNodeOperationType.SetText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
    }

    [Fact]
    public void Teleport_EnabledSelectorTarget_MountsIntoRegisteredRoot()
    {
        TestElement container = _renderer.CreateContainer();
        TestElement target = _renderer.CreateContainer("aside");
        target.Properties["id"] = "modal";
        _renderer.RegisterQueryRoot(target);

        _renderer.Render(
            ComponentTree.Teleport(
                "#modal",
                [
                    ComponentTree.Element(
                        "p",
                        children: [ComponentTree.Text("teleported")]),
                ]),
            container);

        TestNodeSerializer.Serialize(container).ShouldBe(
            "<root><!--teleport start--><!--teleport end--></root>");
        TestNodeSerializer.Serialize(target).ShouldBe(
            "<aside id=\"modal\"><p>teleported</p></aside>");
    }

    [Fact]
    public void Teleport_Disabled_RendersAtLogicalPosition()
    {
        TestElement container = _renderer.CreateContainer();
        TestElement target = _renderer.CreateContainer("aside");
        target.Properties["id"] = "modal";
        _renderer.RegisterQueryRoot(target);

        _renderer.Render(
            ComponentTree.Teleport(
                "#modal",
                [
                    ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text("local")]),
                ],
                isDisabled: true),
            container);

        TestNodeSerializer.Serialize(container).ShouldBe(
            "<root><!--teleport start--><span>local</span><!--teleport end--></root>");
        TestNodeSerializer.Serialize(target).ShouldBe(
            "<aside id=\"modal\"></aside>");
    }

    [Fact]
    public void Teleport_TargetChange_MovesExistingChildrenBetweenRegisteredRoots()
    {
        TestElement container = _renderer.CreateContainer();
        TestElement firstTarget = _renderer.CreateContainer("aside");
        TestElement secondTarget = _renderer.CreateContainer("aside");
        firstTarget.Properties["id"] = "first";
        secondTarget.Properties["id"] = "second";
        _renderer.RegisterQueryRoot(firstTarget);
        _renderer.RegisterQueryRoot(secondTarget);
        IComponent child = ComponentTree.Element(
            "p",
            children: [ComponentTree.Text("content")]);
        _renderer.Render(
            ComponentTree.Teleport("#first", [child]),
            container);
        TestNode teleportedElement = firstTarget.Children[0];

        _renderer.Render(
            ComponentTree.Teleport("#second", [child]),
            container);

        TestNodeSerializer.Serialize(firstTarget).ShouldBe(
            "<aside id=\"first\"></aside>");
        TestNodeSerializer.Serialize(secondTarget).ShouldBe(
            "<aside id=\"second\"><p>content</p></aside>");
        secondTarget.Children[0].ShouldBeSameAs(teleportedElement);
    }

    [Fact]
    public void Serializer_OmitsNullAndEventBindings()
    {
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "div",
                Attributes(
                    ("id", "a"),
                    ("onClick", (Action)(() => { })),
                    ("hidden", null)),
                [
                    ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text("inner")]),
                ]),
            container);

        TestNodeSerializer.Serialize(container.Children[0])
            .ShouldBe("<div id=\"a\"><span>inner</span></div>");
    }

    [Fact]
    public void TriggerEvent_InvokesRegisteredSynchronousListener()
    {
        int clicks = 0;
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", (Action)(() => clicks++))),
                [ComponentTree.Text("press")]),
            container);

        TestElement button = (TestElement)container.Children[0];
        TestEventDispatcher.Trigger(button, "click").ShouldBeTrue();
        clicks.ShouldBe(1);
    }

    [Fact]
    public async Task TriggerEventAsync_AwaitsTaskReturningListener()
    {
        int clicks = 0;
        async Task ClickAsync()
        {
            await Task.Yield();
            clicks++;
        }

        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", (Func<Task>)ClickAsync)),
                [ComponentTree.Text("press")]),
            container);

        bool handled = await TestEventDispatcher.TriggerAsync(
            (TestElement)container.Children[0],
            "click");

        handled.ShouldBeTrue();
        clicks.ShouldBe(1);
    }

    [Fact]
    public void TriggerEvent_PassesPayloadAndInvokesMulticastListenersInOrder()
    {
        List<object?> received = [];
        Action<object?> first = payload => received.Add(payload);
        Action<object?> second = payload => received.Add($"second:{payload}");
        Action<object?> listeners = first + second;
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "input",
                Attributes(("onInput", listeners))),
            container);

        TestEventDispatcher.Trigger(
            (TestElement)container.Children[0],
            "input",
            "hello").ShouldBeTrue();

        received.ShouldBe(new object?[] { "hello", "second:hello" });
    }

    [Fact]
    public void TriggerEvent_AfterListenerRemoval_ReturnsFalse()
    {
        int clicks = 0;
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", (Action)(() => clicks++)))),
            container);
        TestElement button = (TestElement)container.Children[0];

        _renderer.Render(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", null))),
            container);

        TestEventDispatcher.Trigger(button, "click").ShouldBeFalse();
        clicks.ShouldBe(0);
    }

    [Fact]
    public void TriggerEvent_UnknownEvent_ReturnsFalse()
    {
        TestElement container = _renderer.CreateContainer();
        _renderer.Render(ComponentTree.Element("div"), container);

        TestEventDispatcher.Trigger(
            (TestElement)container.Children[0],
            "click").ShouldBeFalse();
    }

    [Fact]
    public void SchedulerPump_CapturesFlushesUntilPumped()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        bool ran = false;

        Scheduler.QueueJob(new SchedulerJob(() => ran = true));

        ran.ShouldBeFalse();
        pump.PendingFlushCount.ShouldBe(1);
        pump.RunUntilIdle().ShouldBe(1);
        ran.ShouldBeTrue();
    }

    private static ComponentAttributes Attributes(
        params (string Name, object? Value)[] values)
    {
        List<IComponentAttribute> attributes = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            attributes.Add(
                new ComponentAttribute(
                    values[index].Name,
                    values[index].Value));
        }

        return new ComponentAttributes(attributes);
    }
}
