using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins optimized block teardown to Vue 3.5's dynamic-child and v-once behavior.
/// </summary>
public sealed class BlockUnmountParityTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public BlockUnmountParityTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        Renderer<FakeHostNode>.UnmountVisitCount = 0;
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Unmount_ElementBlock_VisitsOnlyRootAndDynamicChild()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent dynamic = DynamicElement("span", "live");
        IComponent block = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Element("header"),
                ComponentTree.Element("nav"),
                ComponentTree.Element("footer"),
                dynamic,
            ],
            optimization: Block(dynamic));
        renderer.Render(block, host.Root);
        Renderer<FakeHostNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Root);

        Renderer<FakeHostNode>.UnmountVisitCount.ShouldBe(2);
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Unmount_DynamicElement_DoesNotRewalkItsStaticChildren()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent dynamic = ComponentTree.Element(
            "section",
            children:
            [
                ComponentTree.Element("span"),
                ComponentTree.Element("span"),
            ],
            optimization: new ComponentOptimization(PatchFlags.Class));
        IComponent block = ComponentTree.Element(
            "div",
            children: [dynamic],
            optimization: Block(dynamic));
        renderer.Render(block, host.Root);
        Renderer<FakeHostNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Root);

        Renderer<FakeHostNode>.UnmountVisitCount.ShouldBe(2);
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Unmount_StableFragmentBlock_VisitsOnlyFragmentAndDynamicChild()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent dynamic = DynamicElement("span", "live");
        IComponent block = ComponentTree.Fragment(
            children:
            [
                ComponentTree.Element("hr"),
                dynamic,
            ],
            optimization: new ComponentOptimization(
                PatchFlags.StableFragment,
                dynamicChildren: [dynamic]));
        renderer.Render(block, host.Root);
        Renderer<FakeHostNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Root);

        Renderer<FakeHostNode>.UnmountVisitCount.ShouldBe(2);
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Unmount_DynamicChild_PreservesNodeLifecycleTeardown()
    {
        List<string> events = [];
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ComponentNodeLifecycleHook beforeUnmount =
            (_, _) => events.Add("before-unmount");
        ComponentNodeLifecycleHook unmounted =
            (_, _) => events.Add("unmounted");
        IElementComponent dynamic = ComponentTree.Element(
            "span",
            new ComponentAttributes(
            [
                new ComponentAttribute(
                    "onVnodeBeforeUnmount",
                    beforeUnmount),
                new ComponentAttribute(
                    "onVnodeUnmounted",
                    unmounted),
            ]),
            optimization: new ComponentOptimization(PatchFlags.NeedPatch));
        IComponent block = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Element("header"),
                dynamic,
            ],
            optimization: Block(dynamic));
        renderer.Render(block, host.Root);

        renderer.Render(null, host.Root);

        events.Count.ShouldBe(2);
        events[0].ShouldBe("before-unmount");
        events[1].ShouldBe("unmounted");
    }

    [Fact]
    public void Unmount_HasOnceBlock_FullyWalksUncollectedLifecycleChild()
    {
        List<string> events = [];
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent dynamic = DynamicElement("span", "live");
        ComponentNodeLifecycleHook unmounted =
            (_, _) => events.Add("once-unmounted");
        IElementComponent once = ComponentTree.Element(
            "i",
            new ComponentAttributes(
            [
                new ComponentAttribute(
                    "onVnodeUnmounted",
                    unmounted),
            ]));
        IComponent block = ComponentTree.Element(
            "div",
            children: [dynamic, once],
            optimization: new ComponentOptimization(
                dynamicChildren: [dynamic],
                hasOnce: true));
        renderer.Render(block, host.Root);
        Renderer<FakeHostNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Root);

        events.Count.ShouldBe(1);
        events[0].ShouldBe("once-unmounted");
        Renderer<FakeHostNode>.UnmountVisitCount.ShouldBe(4);
        host.Root.Children.ShouldBeEmpty();
    }

    private static IElementComponent DynamicElement(
        string tag,
        string text)
    {
        return ComponentTree.Element(
            tag,
            children: [ComponentTree.Text(text)],
            optimization: new ComponentOptimization(PatchFlags.Text));
    }

    private static ComponentOptimization Block(
        params IComponent[] dynamicChildren)
    {
        return new ComponentOptimization(
            dynamicChildren: dynamicChildren);
    }
}
