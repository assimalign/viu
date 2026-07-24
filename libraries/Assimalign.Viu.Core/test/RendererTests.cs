using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererTests
{
    [Fact]
    public void Render_ComponentNodeLifecycleHooks_FireInPipelineOrder()
    {
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> events = [];
        IElementComponent? mountedComponent = null;
        IElementComponent? previousOnUpdate = null;
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IElementComponent initial = ComponentTree.Element(
            "div",
            LifecycleAttributes(
                events,
                (component, previous) =>
                {
                    events.Add("mounted");
                    mountedComponent = component.ShouldBeAssignableTo<IElementComponent>();
                    previous.ShouldBeNull();
                }),
            children: [ComponentTree.Text("before")]);

        renderer.Render(initial, host.Root);
        pump.RunUntilIdle();

        events.ShouldBe(["before-mount", "mounted"]);
        mountedComponent.ShouldBeSameAs(initial);
        host.Root.Children.Single().Attributes.ShouldNotContainKey(
            "onVnodeMounted");

        events.Clear();
        IElementComponent next = ComponentTree.Element(
            "div",
            LifecycleAttributes(
                events,
                beforeUpdate: (component, previous) =>
                {
                    events.Add("before-update");
                    previousOnUpdate =
                        previous.ShouldBeAssignableTo<IElementComponent>();
                    component.ShouldBeAssignableTo<IElementComponent>();
                }),
            children: [ComponentTree.Text("after")]);
        renderer.Render(next, host.Root);
        pump.RunUntilIdle();

        events.ShouldBe(["before-update", "updated"]);
        previousOnUpdate.ShouldBeSameAs(initial);

        events.Clear();
        FakeHostNode element = host.Root.Children.Single();
        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        events.ShouldBe(["before-unmount", "unmounted"]);
        element.Parent.ShouldBeNull();
        Scheduler.Reset();
    }

    [Fact]
    public void Render_ElementMountPatchAndUnmount_PreservesHostIdentityAndPatchesAttributes()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent current = ComponentTree.Element(
            "main",
            new ComponentAttributes(
            [
                new ComponentAttribute("class", "before"),
                new ComponentAttribute("stale", true),
            ]),
            [
                ComponentTree.Text("hello"),
                ComponentTree.Comment("first"),
            ]);

        renderer.Render(current, host.Root);

        FakeHostNode element = host.Root.Children.Single();
        FakeHostNode text = element.Children[0];
        FakeHostNode comment = element.Children[1];
        element.Attributes["class"].ShouldBe("before");
        element.Attributes["stale"].ShouldBe(true);

        host.Operations.Clear();
        IElementComponent next = ComponentTree.Element(
            "main",
            new ComponentAttributes(
            [
                new ComponentAttribute("class", "after"),
                new ComponentAttribute("title", "updated"),
            ]),
            [
                ComponentTree.Text("goodbye"),
                ComponentTree.Comment("second"),
            ]);

        renderer.Render(next, host.Root);

        host.Root.Children.Single().ShouldBeSameAs(element);
        element.Children[0].ShouldBeSameAs(text);
        element.Children[1].ShouldBeSameAs(comment);
        text.Content.ShouldBe("goodbye");
        comment.Content.ShouldBe("first");
        element.Attributes.ShouldNotContainKey("stale");
        element.Attributes["class"].ShouldBe("after");
        element.Attributes["title"].ShouldBe("updated");
        host.Operations.ShouldContain(
            $"attribute:{element.Identifier}:stale:True:null");
        host.Operations.ShouldContain(
            $"attribute:{element.Identifier}:class:before:after");
        host.Operations.ShouldContain(
            $"text:{text.Identifier}:hello:goodbye");
        current.Attributes.TryGetValue("class", out object? oldClass).ShouldBeTrue();
        oldClass.ShouldBe("before");

        host.Operations.Clear();
        renderer.Render(null, host.Root);

        host.Root.Children.ShouldBeEmpty();
        host.Operations.ShouldBe([$"remove:{element.Identifier}"]);
    }

    private static ComponentAttributes LifecycleAttributes(
        List<string> events,
        ComponentNodeLifecycleHook? mounted = null,
        ComponentNodeLifecycleHook? beforeUpdate = null)
    {
        return new ComponentAttributes(
        [
            new ComponentAttribute(
                "onVnodeBeforeMount",
                (ComponentNodeLifecycleHook)(
                    (_, _) => events.Add("before-mount"))),
            new ComponentAttribute(
                "onVnodeMounted",
                mounted
                    ?? ((_, _) => events.Add("mounted"))),
            new ComponentAttribute(
                "onVnodeBeforeUpdate",
                beforeUpdate
                    ?? ((_, _) => events.Add("before-update"))),
            new ComponentAttribute(
                "onVnodeUpdated",
                (ComponentNodeLifecycleHook)(
                    (_, _) => events.Add("updated"))),
            new ComponentAttribute(
                "onVnodeBeforeUnmount",
                (ComponentNodeLifecycleHook)(
                    (_, _) => events.Add("before-unmount"))),
            new ComponentAttribute(
                "onVnodeUnmounted",
                (ComponentNodeLifecycleHook)(
                    (_, _) => events.Add("unmounted"))),
        ]);
    }

    [Fact]
    public void Render_KeyedChildren_ReordersExistingHostNodesWithMinimalMove()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Fragment(
            [
                KeyedElement("a", "A"),
                KeyedElement("b", "B"),
                KeyedElement("c", "C"),
            ]),
            host.Root);
        FakeHostNode[] original = Elements(host.Root);
        Dictionary<string, FakeHostNode> byText = original.ToDictionary(host.Text);

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
            [
                KeyedElement("c", "C"),
                KeyedElement("a", "A"),
                KeyedElement("b", "B"),
            ]),
            host.Root);

        FakeHostNode[] reordered = Elements(host.Root);
        reordered.Select(host.Text).ShouldBe(["C", "A", "B"]);
        reordered[0].ShouldBeSameAs(byText["C"]);
        reordered[1].ShouldBeSameAs(byText["A"]);
        reordered[2].ShouldBeSameAs(byText["B"]);
        host.Operations.Count(operation => operation.StartsWith("insert:", StringComparison.Ordinal))
            .ShouldBe(1);
        host.Operations.Single(operation => operation.StartsWith("insert:", StringComparison.Ordinal))
            .ShouldStartWith($"insert:{byText["C"].Identifier}:");
    }

    [Fact]
    public void Render_UnkeyedFragment_PatchesChildrenPositionallyWithoutMoves()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Fragment(
                [
                    ComponentTree.Element("li", children: [ComponentTree.Text("A")]),
                    ComponentTree.Element("li", children: [ComponentTree.Text("B")]),
                ],
                optimization: new ComponentOptimization(PatchFlags.UnkeyedFragment)),
            host.Root);
        FakeHostNode[] original = Elements(host.Root);

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
                [
                    ComponentTree.Element("li", children: [ComponentTree.Text("B")]),
                    ComponentTree.Element("li", children: [ComponentTree.Text("A")]),
                ],
                optimization: new ComponentOptimization(PatchFlags.UnkeyedFragment)),
            host.Root);

        FakeHostNode[] patched = Elements(host.Root);
        patched[0].ShouldBeSameAs(original[0]);
        patched[1].ShouldBeSameAs(original[1]);
        patched.Select(host.Text).ShouldBe(["B", "A"]);
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("insert:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_KeyedChildren_MountsAtAnchorAndUnmountsRemovedIdentity()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Fragment(
            [
                KeyedElement("a", "A"),
                KeyedElement("c", "C"),
            ]),
            host.Root);
        FakeHostNode a = Elements(host.Root)[0];
        FakeHostNode c = Elements(host.Root)[1];

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
            [
                KeyedElement("a", "A"),
                KeyedElement("b", "B"),
                KeyedElement("c", "C"),
            ]),
            host.Root);

        FakeHostNode[] inserted = Elements(host.Root);
        FakeHostNode b = inserted[1];
        inserted.ShouldBe([a, b, c]);
        host.Operations.ShouldContain(
            $"insert:{b.Identifier}:{host.Root.Identifier}:{c.Identifier}");

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
            [
                KeyedElement("c", "C"),
                KeyedElement("b", "B"),
            ]),
            host.Root);

        Elements(host.Root).ShouldBe([c, b]);
        a.Parent.ShouldBeNull();
        host.Operations.ShouldContain($"remove:{a.Identifier}");
    }

    [Fact]
    public void Render_KeyedStaticRange_MovePreservesEveryNodeAndFragmentAnchors()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Fragment(
            [
                ComponentTree.Static("a1|a2", key: "a"),
                ComponentTree.Static("b1|b2", key: "b"),
            ]),
            host.Root);
        FakeHostNode startAnchor = host.Root.Children[0];
        FakeHostNode endAnchor = host.Root.Children[^1];
        FakeHostNode[] staticNodes = host.Root.Children
            .Where(node => node.Kind == FakeHostNodeKind.Static)
            .ToArray();

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
            [
                ComponentTree.Static("b1|b2", key: "b"),
                ComponentTree.Static("a1|a2", key: "a"),
            ]),
            host.Root);

        host.Root.Children[0].ShouldBeSameAs(startAnchor);
        host.Root.Children[^1].ShouldBeSameAs(endAnchor);
        FakeHostNode[] reordered = host.Root.Children
            .Where(node => node.Kind == FakeHostNodeKind.Static)
            .ToArray();
        reordered.Select(node => node.Content).ShouldBe(["b1", "b2", "a1", "a2"]);
        reordered.ShouldBe(
        [
            staticNodes[2],
            staticNodes[3],
            staticNodes[0],
            staticNodes[1],
        ]);
        host.Operations.Count(operation => operation.StartsWith("insert:", StringComparison.Ordinal))
            .ShouldBe(2);

        renderer.Render(null, host.Root);
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_NullAndEmptyDynamicChildren_HaveDistinctPatchSemantics()
    {
        FakeHost unoptimizedHost = new();
        Renderer<FakeHostNode> unoptimizedRenderer =
            RendererFactory.CreateRenderer(unoptimizedHost.Options);
        unoptimizedRenderer.Render(
            ComponentTree.Element(
                "section",
                children: [ComponentTree.Text("before")]),
            unoptimizedHost.Root);

        Renderer<FakeHostNode>.PatchVisitCount = 0;
        unoptimizedRenderer.Render(
            ComponentTree.Element(
                "section",
                children: [ComponentTree.Text("after")]),
            unoptimizedHost.Root);

        unoptimizedHost.Text(unoptimizedHost.Root.Children.Single()).ShouldBe("after");
        Renderer<FakeHostNode>.PatchVisitCount.ShouldBe(2);

        FakeHost optimizedHost = new();
        Renderer<FakeHostNode> optimizedRenderer =
            RendererFactory.CreateRenderer(optimizedHost.Options);
        optimizedRenderer.Render(
            ComponentTree.Element(
                "section",
                new ComponentAttributes(
                [
                    new ComponentAttribute("class", "before"),
                ]),
                [ComponentTree.Text("fixed")],
                optimization: new ComponentOptimization(
                    PatchFlags.Class,
                    dynamicChildren: Array.Empty<IComponent>())),
            optimizedHost.Root);

        Renderer<FakeHostNode>.PatchVisitCount = 0;
        optimizedRenderer.Render(
            ComponentTree.Element(
                "section",
                new ComponentAttributes(
                [
                    new ComponentAttribute("class", "after"),
                ]),
                [ComponentTree.Text("must-not-patch")],
                optimization: new ComponentOptimization(
                    PatchFlags.Class,
                    dynamicChildren: Array.Empty<IComponent>())),
            optimizedHost.Root);

        FakeHostNode optimizedElement = optimizedHost.Root.Children.Single();
        optimizedHost.Text(optimizedElement).ShouldBe("fixed");
        optimizedElement.Attributes["class"].ShouldBe("after");
        Renderer<FakeHostNode>.PatchVisitCount.ShouldBe(1);
    }

    [Fact]
    public void Render_BlockWithTextFlagAndEmptyDynamicChildren_PatchesText()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Element(
                "button",
                children: [ComponentTree.Text("Count: 0")],
                optimization: new ComponentOptimization(
                    PatchFlags.Text,
                    dynamicChildren: Array.Empty<IComponent>())),
            host.Root);

        renderer.Render(
            ComponentTree.Element(
                "button",
                children: [ComponentTree.Text("Count: 1")],
                optimization: new ComponentOptimization(
                    PatchFlags.Text,
                    dynamicChildren: Array.Empty<IComponent>())),
            host.Root);

        host.Text(host.Root.Children.Single()).ShouldBe("Count: 1");
    }

    [Fact]
    public void Render_BlockWithDynamicDescendant_VisitsOnlyRootAndDynamicNode()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ITextComponent currentDynamic = ComponentTree.Text(
            "before",
            new ComponentOptimization(PatchFlags.Text));
        renderer.Render(
            ComponentTree.Element(
                "section",
                children:
                [
                    ComponentTree.Text("fixed"),
                    currentDynamic,
                ],
                optimization: new ComponentOptimization(
                    dynamicChildren: new IComponent[] { currentDynamic })),
            host.Root);

        ITextComponent nextDynamic = ComponentTree.Text(
            "after",
            new ComponentOptimization(PatchFlags.Text));
        Renderer<FakeHostNode>.PatchVisitCount = 0;
        renderer.Render(
            ComponentTree.Element(
                "section",
                children:
                [
                    ComponentTree.Text("must-not-patch"),
                    nextDynamic,
                ],
                optimization: new ComponentOptimization(
                    dynamicChildren: new IComponent[] { nextDynamic })),
            host.Root);

        FakeHostNode element = host.Root.Children.Single();
        element.Children.Select(node => node.Content).ShouldBe(["fixed", "after"]);
        Renderer<FakeHostNode>.PatchVisitCount.ShouldBe(2);
    }

    [Fact]
    public void Render_BlockShapeMismatch_FallsBackToFullChildrenDiff()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            ComponentTree.Element(
                "section",
                children: [ComponentTree.Text("before")],
                optimization: new ComponentOptimization(
                    dynamicChildren: Array.Empty<IComponent>())),
            host.Root);

        ITextComponent nextText = ComponentTree.Text("after");
        Renderer<FakeHostNode>.PatchVisitCount = 0;
        renderer.Render(
            ComponentTree.Element(
                "section",
                children: [nextText],
                optimization: new ComponentOptimization(
                    dynamicChildren: new IComponent[] { nextText })),
            host.Root);

        host.Text(host.Root.Children.Single()).ShouldBe("after");
        Renderer<FakeHostNode>.PatchVisitCount.ShouldBe(2);
    }

    [Fact]
    public void Render_BlockDynamicTypeReplacement_UnmountsTheReplacementReference()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        object? replacementReference = null;
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IElementComponent currentDynamic = ComponentTree.Element("span");
        renderer.Render(
            ComponentTree.Element(
                "section",
                children: [currentDynamic],
                optimization: new ComponentOptimization(
                    dynamicChildren: new IComponent[] { currentDynamic })),
            host.Root);
        pump.RunUntilIdle();

        IElementComponent nextDynamic = ComponentTree.Element(
            "strong",
            reference: TemplateReference.FromCallback(
                value => replacementReference = value));
        renderer.Render(
            ComponentTree.Element(
                "section",
                children: [nextDynamic],
                optimization: new ComponentOptimization(
                    dynamicChildren: new IComponent[] { nextDynamic })),
            host.Root);
        pump.RunUntilIdle();

        replacementReference.ShouldBeSameAs(
            host.Root.Children.Single().Children.Single());

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        replacementReference.ShouldBeNull();
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ValueTypeHostHandle_UsesDefaultAsMissingNode()
    {
        const int root = 1;
        int nextIdentifier = root;
        Dictionary<int, List<int>> children = new()
        {
            [root] = [],
        };
        Dictionary<int, int> parents = [];
        Dictionary<int, string> content = [];

        int Create(string value)
        {
            int identifier = ++nextIdentifier;
            children[identifier] = [];
            content[identifier] = value;
            return identifier;
        }

        RendererOptions<int> options = new()
        {
            Insert = (child, parent, anchor) =>
            {
                if (parents.TryGetValue(child, out int previousParent))
                {
                    children[previousParent].Remove(child);
                }

                int index = anchor == default
                    ? children[parent].Count
                    : children[parent].IndexOf(anchor);
                children[parent].Insert(index, child);
                parents[child] = parent;
            },
            Remove = node =>
            {
                if (parents.Remove(node, out int parent))
                {
                    children[parent].Remove(node);
                }
            },
            CreateElement = (tag, _) => Create(tag),
            CreateText = Create,
            CreateComment = Create,
            SetText = (node, value) => content[node] = value,
            ParentNode = node => parents.GetValueOrDefault(node),
            NextSibling = node =>
            {
                if (!parents.TryGetValue(node, out int parent))
                {
                    return default;
                }

                int index = children[parent].IndexOf(node);
                return index + 1 < children[parent].Count
                    ? children[parent][index + 1]
                    : default;
            },
            PatchAttribute = (_, _, _, _, _, _) => { },
        };
        Renderer<int> renderer = RendererFactory.CreateRenderer(options);

        renderer.Render(ComponentTree.Text("before"), root);
        int textHandle = children[root].Single();
        renderer.Render(ComponentTree.Text("after"), root);

        children[root].Single().ShouldBe(textHandle);
        content[textHandle].ShouldBe("after");

        renderer.Render(null, root);
        children[root].ShouldBeEmpty();
    }

    [Fact]
    public void Render_Teleport_TogglesLogicalPlacementAndRetargetsExistingChildren()
    {
        FakeHost host = new();
        FakeHostNode firstTarget = host.CreateContainer("first-target");
        FakeHostNode secondTarget = host.CreateContainer("second-target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            ComponentTree.Teleport(
                firstTarget,
                [KeyedElement("content", "teleported")]),
            host.Root);

        host.Root.Children.Count.ShouldBe(2);
        FakeHostNode teleported = Elements(firstTarget).Single();
        host.Text(teleported).ShouldBe("teleported");

        renderer.Render(
            ComponentTree.Teleport(
                firstTarget,
                [KeyedElement("content", "teleported")],
                isDisabled: true),
            host.Root);

        Elements(firstTarget).ShouldBeEmpty();
        Elements(host.Root).Single().ShouldBeSameAs(teleported);

        renderer.Render(
            ComponentTree.Teleport(
                secondTarget,
                [KeyedElement("content", "teleported")]),
            host.Root);

        Elements(host.Root).ShouldBeEmpty();
        Elements(secondTarget).Single().ShouldBeSameAs(teleported);

        renderer.Render(null, host.Root);

        host.Root.Children.ShouldBeEmpty();
        firstTarget.Children.ShouldBeEmpty();
        secondTarget.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_UnresolvedTeleportTarget_KeepsLogicalAnchorsAndDefersChildren()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        Should.NotThrow(
            () => renderer.Render(
                ComponentTree.Teleport(
                    "#missing",
                    [ComponentTree.Element("span")]),
                host.Root));

        host.Root.Children.Count.ShouldBe(2);
        Elements(host.Root).ShouldBeEmpty();
        renderer.Render(null, host.Root);
        host.Root.Children.ShouldBeEmpty();
    }

    private static IElementComponent KeyedElement(string key, string text)
    {
        return ComponentTree.Element(
            "li",
            children: [ComponentTree.Text(text)],
            key: key);
    }

    private static FakeHostNode[] Elements(FakeHostNode root)
    {
        return root.Children
            .Where(node => node.Kind == FakeHostNodeKind.Element)
            .ToArray();
    }
}
