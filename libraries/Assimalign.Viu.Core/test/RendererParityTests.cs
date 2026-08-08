using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererParityTests
{
    [Fact]
    public void Render_MountPatchAndUnmount_PinsVisitCounters()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        ElementNode initial = Element(
            "root",
            new TextNode("first"),
            new TextNode("second"));

        renderer.Render(initial, host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(0);
        Renderer<RendererParityNode>.UnmountVisitCount.ShouldBe(0);
        host.Container.DescendantText.ShouldBe("firstsecond");

        renderer.Render(
            Element(
                "root",
                new TextNode("updated first"),
                new TextNode("updated second")),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(3);
        host.Container.DescendantText.ShouldBe("updated firstupdated second");

        renderer.Render(null, host.Container);

        Renderer<RendererParityNode>.UnmountVisitCount.ShouldBe(3);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_PositiveFlagWithoutText_SkipsChildWalk()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        RenderPlan plan = new(PatchFlags.NeedPatch);
        renderer.Render(
            Element("root", plan, new TextNode("before")),
            host.Container);
        host.ResetOperationCounts();

        renderer.Render(
            Element("root", new RenderPlan(PatchFlags.NeedPatch), new TextNode("after")),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(1);
        host.TextChangeCount.ShouldBe(0);
        host.Container.DescendantText.ShouldBe("before");
    }

    [Fact]
    public void Render_BlockWithEmptyDynamicChildren_SkipsEveryChildVisit()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        RenderPlan previousPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: Array.Empty<VirtualNode>());
        renderer.Render(
            Element("root", previousPlan, new TextNode("compiler static")),
            host.Container);
        host.ResetOperationCounts();
        RenderPlan nextPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: Array.Empty<VirtualNode>());

        renderer.Render(
            Element("root", nextPlan, new TextNode("must be skipped")),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(1);
        host.TextChangeCount.ShouldBe(0);
        host.Container.DescendantText.ShouldBe("compiler static");
    }

    [Fact]
    public void Render_BlockWithDynamicChild_PatchesOnlyListedDescendant()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        var previousStatic = new TextNode("static before|");
        var previousDynamic = new TextNode("dynamic before");
        RenderPlan previousPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: new VirtualNode[] { previousDynamic });
        renderer.Render(
            Element("root", previousPlan, previousStatic, previousDynamic),
            host.Container);
        host.ResetOperationCounts();
        var nextStatic = new TextNode("static after|");
        var nextDynamic = new TextNode("dynamic after");
        RenderPlan nextPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: new VirtualNode[] { nextDynamic });

        renderer.Render(
            Element("root", nextPlan, nextStatic, nextDynamic),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(2);
        host.TextChangeCount.ShouldBe(1);
        host.Container.DescendantText.ShouldBe("static before|dynamic after");
    }

    [Fact]
    public void Render_CachedWholeValue_SkipsBindingsAndChildren()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        renderer.Render(
            ElementWithBinding("before", new TextNode("before"), RenderPlan.None),
            host.Container);
        host.ResetOperationCounts();

        renderer.Render(
            ElementWithBinding(
                "after",
                new TextNode("after"),
                new RenderPlan(PatchFlags.Cached)),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(1);
        host.BindingPatchCount.ShouldBe(0);
        host.TextChangeCount.ShouldBe(0);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        element.Bindings["data-value"].ShouldBe("before");
        element.DescendantText.ShouldBe("before");
    }

    [Fact]
    public void Render_BailWholeValue_UsesFullDiffDespiteBlockMetadata()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        RenderPlan previousPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: Array.Empty<VirtualNode>());
        renderer.Render(
            ElementWithBinding("before", new TextNode("before"), previousPlan),
            host.Container);
        host.ResetOperationCounts();
        RenderPlan nextPlan = new(
            PatchFlags.Bail,
            dynamicChildren: Array.Empty<VirtualNode>());

        renderer.Render(
            ElementWithBinding("after", new TextNode("after"), nextPlan),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(2);
        host.BindingPatchCount.ShouldBe(1);
        host.TextChangeCount.ShouldBe(1);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        element.Bindings["data-value"].ShouldBe("after");
        element.DescendantText.ShouldBe("after");
    }

    [Fact]
    public void Render_MountBindings_AppliesValueLast()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        var value = new ElementNode(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Property("value", "text"),
                ElementBinding.Attribute(new QualifiedName("type"), "search"),
                ElementBinding.Attribute(new QualifiedName("data-order"), "middle"),
            ]);

        renderer.Render(value, host.Container);

        host.BindingPatchNames.ShouldBe(["type", "data-order", "value"]);
    }

    [Fact]
    public void Render_EqualValueBinding_WritesValueAgain()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(ElementWithEqualBindings(), host.Container);
        host.ResetOperationCounts();

        renderer.Render(ElementWithEqualBindings(), host.Container);

        host.BindingPatchCount.ShouldBe(1);
        host.BindingPatchNames.ShouldBe(["value"]);
    }

    [Fact]
    public void Render_TextFlag_PatchesOnlyTheTextPayload()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        renderer.Render(
            ElementWithBinding("before", new TextNode("before"), RenderPlan.None),
            host.Container);
        host.ResetOperationCounts();

        renderer.Render(
            ElementWithBinding(
                "ignored binding change",
                new TextNode("after"),
                new RenderPlan(PatchFlags.Text)),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(2);
        host.BindingPatchCount.ShouldBe(0);
        host.TextChangeCount.ShouldBe(1);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        element.Bindings["data-value"].ShouldBe("before");
        element.DescendantText.ShouldBe("after");
    }

    [Fact]
    public void Render_IncompatibleBlocks_ForceFullBindingAndChildDiff()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        RenderPlan previousPlan = new(
            PatchFlags.NeedPatch,
            dynamicChildren: Array.Empty<VirtualNode>());
        renderer.Render(
            ElementWithBinding("before", new TextNode("before"), previousPlan),
            host.Container);
        host.ResetOperationCounts();
        var nextText = new TextNode("after");
        RenderPlan nextPlan = new(
            PatchFlags.Class,
            dynamicChildren: new VirtualNode[] { nextText });

        renderer.Render(
            ElementWithBinding("after", nextText, nextPlan),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(2);
        host.BindingPatchCount.ShouldBe(1);
        host.TextChangeCount.ShouldBe(1);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        element.Bindings["data-value"].ShouldBe("after");
        element.DescendantText.ShouldBe("after");
    }

    [Fact]
    public void Render_NodeLifecycleBindings_NeverReachHostBindingLayer()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        var initial = new ElementNode(
            new QualifiedName("root"),
            bindings:
            [
                ElementBinding.Attribute(
                    new QualifiedName("onVnodeMounted"),
                    new VirtualNodeLifecycleHook((_, _) => { })),
            ]);

        renderer.Render(initial, host.Container);
        host.ResetOperationCounts();
        var next = new ElementNode(
            new QualifiedName("root"),
            bindings:
            [
                ElementBinding.Attribute(
                    new QualifiedName("onVnodeMounted"),
                    new VirtualNodeLifecycleHook((_, _) => { })),
            ]);
        renderer.Render(next, host.Container);

        host.BindingPatchCount.ShouldBe(0);
        host.Container.Children.ShouldHaveSingleItem().Bindings.ShouldBeEmpty();
    }

    [Fact]
    public void Render_KeyedReorder_MovesOnlyNodesOutsideLongestIncreasingSubsequence()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        renderer.Render(
            KeyedList("A", "B", "C", "D", "E"),
            host.Container);
        host.ResetOperationCounts();

        renderer.Render(
            KeyedList("B", "C", "A", "D", "E"),
            host.Container);

        host.MoveCount.ShouldBe(1);
        host.Container.DescendantText.ShouldBe("BCADE");
    }

    [Fact]
    public void GetMountedComponentViews_RepeatedQueriesAndPatch_PreserveViewIdentity()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        ComponentReference reference = ComponentReference.ForType(
            typeof(RendererViewIdentityComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new RendererViewIdentityComponent()));
        var initialRequest = new ComponentNode(reference);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = initialRequest,
                Components = components,
            });

        renderer.Render(initialRequest, host.Container, application);

        MountedComponentView<RendererParityNode> first =
            renderer.GetMountedComponentViews(host.Container).ShouldHaveSingleItem();
        MountedComponentView<RendererParityNode> repeated =
            renderer.GetMountedComponentViews(host.Container).ShouldHaveSingleItem();
        repeated.ShouldBeSameAs(first);
        first.IsMounted.ShouldBeTrue();
        RendererParityNode firstHostNode = first.FirstHostNode.ShouldNotBeNull();
        var nextRequest = new ComponentNode(reference);

        renderer.Render(nextRequest, host.Container, application);

        MountedComponentView<RendererParityNode> patched =
            renderer.GetMountedComponentViews(host.Container).ShouldHaveSingleItem();
        patched.ShouldBeSameAs(first);
        patched.Request.ShouldBeSameAs(nextRequest);
        patched.FirstHostNode.ShouldBeSameAs(firstHostNode);

        renderer.Render(null, host.Container);

        first.IsMounted.ShouldBeFalse();
        first.FirstHostNode.ShouldBeNull();
        renderer.GetMountedComponentViews(host.Container).ShouldBeEmpty();
    }

    [Fact]
    public void ApplyUpdates_TemplateMarker_RemountsWithFreshInstanceAndNoNodeMapCollision()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ResetVisitCounters();
        int activations = 0;
        ComponentReference reference = ComponentReference.ForType(
            typeof(RendererHotReloadComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new RendererHotReloadComponent(++activations)));
        ComponentHotReload.Register(
            typeof(RendererHotReloadComponent),
            "renderer-parity-hot-reload",
            typeof(RendererHotReloadTemplateMarker),
            typeof(RendererHotReloadScriptMarker),
            typeof(RendererHotReloadStyleMarker));
        var request = new ComponentNode(reference);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = request,
                Components = components,
            });
        renderer.Render(request, host.Container, application);
        MountedComponentView<RendererParityNode> original =
            renderer.GetMountedComponentViews(host.Container).ShouldHaveSingleItem();

        ComponentHotReload.ApplyUpdates([typeof(RendererHotReloadTemplateMarker)]);
        host.RunScheduledFlushes();

        MountedComponentView<RendererParityNode> replacement =
            renderer.GetMountedComponentViews(host.Container).ShouldHaveSingleItem();
        replacement.ShouldNotBeSameAs(original);
        replacement.Instance.ShouldNotBeSameAs(original.Instance);
        original.IsMounted.ShouldBeFalse();
        replacement.IsMounted.ShouldBeTrue();
        activations.ShouldBe(2);
        host.Container.DescendantText.ShouldBe("instance 2");

        renderer.Render(null, host.Container);
    }

    private static void ResetVisitCounters()
    {
        Renderer<RendererParityNode>.PatchVisitCount = 0;
        Renderer<RendererParityNode>.UnmountVisitCount = 0;
    }

    private static ElementNode Element(
        string name,
        params VirtualNode[] children) =>
        Element(name, RenderPlan.None, children);

    private static ElementNode Element(
        string name,
        RenderPlan renderPlan,
        params VirtualNode[] children) =>
        new(
            new QualifiedName(name),
            children: children,
            renderPlan: renderPlan);

    private static ElementNode ElementWithBinding(
        string value,
        VirtualNode child,
        RenderPlan renderPlan) =>
        new(
            new QualifiedName("root"),
            bindings:
            [
                ElementBinding.Attribute(
                    new QualifiedName("data-value"),
                    value),
            ],
            children: new[] { child },
            renderPlan: renderPlan);

    private static ElementNode ElementWithEqualBindings() =>
        new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("data-value"), "same"),
                ElementBinding.Property("value", "same"),
            ]);

    private static ElementNode KeyedList(params string[] keys)
    {
        var children = new List<VirtualNode>(keys.Length);
        for (int index = 0; index < keys.Length; index++)
        {
            string key = keys[index];
            children.Add(
                new ElementNode(
                    new QualifiedName("item"),
                    children: new[] { new TextNode(key) },
                    key: key));
        }

        return new ElementNode(
            new QualifiedName("list"),
            children: children);
    }

    private sealed class RendererViewIdentityComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static _ => new ElementNode(
                new QualifiedName("view-root"),
                children: new[] { new TextNode("view") });
        }
    }

    private sealed class RendererHotReloadComponent : IComponent
    {
        private readonly int _instanceNumber;

        internal RendererHotReloadComponent(int instanceNumber)
        {
            _instanceNumber = instanceNumber;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new TextNode($"instance {_instanceNumber}");
        }
    }

    private sealed class RendererHotReloadTemplateMarker
    {
    }

    private sealed class RendererHotReloadScriptMarker
    {
    }

    private sealed class RendererHotReloadStyleMarker
    {
    }
}
