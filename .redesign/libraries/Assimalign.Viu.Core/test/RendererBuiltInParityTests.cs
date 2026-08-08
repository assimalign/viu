using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins Core's host-neutral Teleport, KeepAlive, Suspense, and Transition executors.
/// </summary>
/// <remarks>
/// Specified by <c>[BLT-1]</c> through <c>[BLT-13]</c>.
/// </remarks>
public sealed class RendererBuiltInParityTests
{
    [Fact]
    public void Teleport_ResolvedTargetAndDisabledPatch_MoveTheSameHostChild()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#overlay");

        renderer.Render(
            new TeleportNode("#overlay", [new TextNode("remote")]),
            host.Container);

        RendererParityNode teleportedText = TextChildren(target).ShouldHaveSingleItem();
        teleportedText.Text.ShouldBe("remote");
        TextChildren(host.Container).ShouldBeEmpty();

        renderer.Render(
            new TeleportNode(
                "#overlay",
                [new TextNode("local")],
                isDisabled: true),
            host.Container);

        TextChildren(target).ShouldBeEmpty();
        RendererParityNode targetAnchor = target.Children.ShouldHaveSingleItem();
        targetAnchor.Kind.ShouldBe(RendererParityNodeKind.Comment);
        targetAnchor.Text.ShouldBe("teleport anchor");
        RendererParityNode logicalText = TextChildren(host.Container).ShouldHaveSingleItem();
        logicalText.ShouldBeSameAs(teleportedText);
        logicalText.Text.ShouldBe("local");
        RendererParityNode nextTarget = host.CreateTeleportTarget("#other-overlay");
        host.ResetOperationCounts();

        renderer.Render(
            new TeleportNode("#other-overlay", [new TextNode("remote-again")]),
            host.Container);

        target.Children.ShouldBeEmpty();
        RendererParityNode movedBack = TextChildren(nextTarget).ShouldHaveSingleItem();
        movedBack.ShouldBeSameAs(teleportedText);
        movedBack.Text.ShouldBe("remote-again");
        host.MoveCount.ShouldBe(1);
        TextChildren(host.Container).ShouldBeEmpty();
    }

    [Fact]
    public void Teleport_DeferredTargetSetup_RunsAfterTheSurroundingRenderJob()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode? target = null;
        bool targetWasEmptyDuringRender = false;
        bool targetWasNotResolvedDuringRender = false;

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        new TeleportNode(
                            "#late",
                            [new TextNode("deferred")],
                            isDeferred: true),
                        host.Container);
                    targetWasNotResolvedDuringRender = host.TeleportResolveCount == 0;
                    target = host.CreateTeleportTarget("#late");
                    targetWasEmptyDuringRender = target.Children.Count == 0;
                }));

        host.RunScheduledFlushes();

        targetWasEmptyDuringRender.ShouldBeTrue();
        targetWasNotResolvedDuringRender.ShouldBeTrue();
        host.TeleportResolveCount.ShouldBe(1);
        TextChildren(target!).ShouldHaveSingleItem().Text.ShouldBe("deferred");
        TextChildren(host.Container).ShouldBeEmpty();
    }

    [Fact]
    public void Teleport_DisabledDeferred_MountsContentAtTheLogicalPositionImmediately()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#disabled");
        bool contentWasLogicalDuringRender = false;
        bool targetHadNoContentDuringRender = false;
        bool targetWasNotResolvedDuringRender = false;

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        new TeleportNode(
                            "#disabled",
                            [new TextNode("local")],
                            isDisabled: true,
                            isDeferred: true),
                        host.Container);
                    contentWasLogicalDuringRender =
                        TextChildren(host.Container).Single().Text == "local";
                    targetHadNoContentDuringRender = target.Children.Count == 0;
                    targetWasNotResolvedDuringRender = host.TeleportResolveCount == 0;
                }));

        host.RunScheduledFlushes();

        contentWasLogicalDuringRender.ShouldBeTrue();
        targetHadNoContentDuringRender.ShouldBeTrue();
        targetWasNotResolvedDuringRender.ShouldBeTrue();
        host.TeleportResolveCount.ShouldBe(1);
        TextChildren(host.Container).ShouldHaveSingleItem().Text.ShouldBe("local");
        TextChildren(target).ShouldBeEmpty();
        RendererParityNode targetAnchor = target.Children.ShouldHaveSingleItem();
        targetAnchor.Kind.ShouldBe(RendererParityNodeKind.Comment);
        targetAnchor.Text.ShouldBe("teleport anchor");
    }

    [Fact]
    public void Teleport_NonDeferredUnresolvedTarget_WarnsAndDoesNotRetryLater()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        List<string> warnings = [];
        TeleportNode initial = new("#too-late", [new TextNode("skipped")]);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                RootComponent = initial,
                Components = new ComponentFactory(),
                WarnHandler = warnings.Add,
            });

        renderer.Render(initial, host.Container, application);
        RendererParityNode target = host.CreateTeleportTarget("#too-late");
        host.RunScheduledFlushes();

        host.TeleportResolveCount.ShouldBe(1);
        target.Children.ShouldBeEmpty();
        warnings.ShouldHaveSingleItem()
            .ShouldContain("Failed to resolve teleport target '#too-late'");
    }

    [Fact]
    public void Teleport_PendingDeferredUpdate_MountsOnlyTheLatestTree()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#latest");
        bool targetWasEmptyDuringRender = false;

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        new TeleportNode(
                            "#latest",
                            [new TextNode("stale")],
                            isDeferred: true),
                        host.Container);
                    renderer.Render(
                        new TeleportNode(
                            "#latest",
                            [new TextNode("latest")],
                            isDeferred: true),
                        host.Container);
                    targetWasEmptyDuringRender = target.Children.Count == 0;
                }));

        host.RunScheduledFlushes();

        targetWasEmptyDuringRender.ShouldBeTrue();
        TextChildren(target).ShouldHaveSingleItem().Text.ShouldBe("latest");
        host.TeleportResolveCount.ShouldBe(1);
    }

    [Fact]
    public void Teleport_UnmountPendingDeferredSetup_CancelsTargetResolution()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#cancelled");

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        new TeleportNode(
                            "#cancelled",
                            [new TextNode("stale")],
                            isDeferred: true),
                        host.Container);
                    renderer.Render(null, host.Container);
                }));

        host.RunScheduledFlushes();

        target.Children.ShouldBeEmpty();
        host.TeleportResolveCount.ShouldBe(0);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Teleport_BlockPlan_PatchesDynamicChildCarriesStaticHostAndFallsBackOnMismatch()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#block");
        ElementNode previousStatic = TeleportBlockElement(
            "static",
            "compiler-static",
            null,
            RenderPlan.None);
        ElementNode previousDynamic = TeleportBlockElement(
            "dynamic",
            "dynamic",
            "one",
            new RenderPlan(PatchFlags.Class));
        TeleportNode initial = new(
            "#block",
            [previousStatic, previousDynamic],
            renderPlan: new RenderPlan(
                PatchFlags.NeedPatch,
                dynamicChildren: [previousDynamic]));

        renderer.Render(initial, host.Container);

        RendererParityNode[] initialElements = ElementChildren(target);
        RendererParityNode staticHost = initialElements[0];
        RendererParityNode dynamicHost = initialElements[1];
        Renderer<RendererParityNode>.PatchVisitCount = 0;
        ElementNode nextStatic = TeleportBlockElement(
            "static",
            "must-be-carried",
            null,
            RenderPlan.None);
        ElementNode nextDynamic = TeleportBlockElement(
            "dynamic",
            "dynamic",
            "two",
            new RenderPlan(PatchFlags.Class));

        renderer.Render(
            new TeleportNode(
                "#block",
                [nextStatic, nextDynamic],
                renderPlan: new RenderPlan(
                    PatchFlags.NeedPatch,
                    dynamicChildren: [nextDynamic])),
            host.Container);

        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(2);
        RendererParityNode[] blockPatchedElements = ElementChildren(target);
        blockPatchedElements[0].ShouldBeSameAs(staticHost);
        blockPatchedElements[0].DescendantText.ShouldBe("compiler-static");
        blockPatchedElements[1].ShouldBeSameAs(dynamicHost);
        blockPatchedElements[1].Bindings["class"].ShouldBe("two");

        renderer.Render(
            new TeleportNode(
                "#block",
                [
                    TeleportBlockElement(
                        "static",
                        "fallback-static",
                        null,
                        RenderPlan.None),
                    TeleportBlockElement(
                        "dynamic",
                        "fallback-dynamic",
                        "three",
                        RenderPlan.None),
                ],
                renderPlan: new RenderPlan(
                    PatchFlags.NeedPatch,
                    dynamicChildren: Array.Empty<VirtualNode>())),
            host.Container);

        RendererParityNode[] fallbackElements = ElementChildren(target);
        fallbackElements[0].ShouldBeSameAs(staticHost);
        fallbackElements[0].DescendantText.ShouldBe("fallback-static");
        fallbackElements[1].ShouldBeSameAs(dynamicHost);
        fallbackElements[1].DescendantText.ShouldBe("fallback-dynamic");
        fallbackElements[1].Bindings["class"].ShouldBe("three");
    }

    [Fact]
    public void Teleport_DeferredBlockUpdate_PreservesStaticHostIdentity()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#deferred-block");
        ElementNode initialStatic = TeleportBlockElement(
            "static",
            "compiler-static",
            null,
            RenderPlan.None);
        ElementNode initialDynamic = TeleportBlockElement(
            "dynamic",
            "dynamic",
            "one",
            new RenderPlan(PatchFlags.Class));
        renderer.Render(
            new TeleportNode(
                "#deferred-block",
                [initialStatic, initialDynamic],
                isDeferred: true,
                renderPlan: new RenderPlan(
                    PatchFlags.NeedPatch,
                    dynamicChildren: [initialDynamic])),
            host.Container);
        host.RunScheduledFlushes();
        RendererParityNode[] initialElements = ElementChildren(target);
        RendererParityNode staticHost = initialElements[0];
        RendererParityNode dynamicHost = initialElements[1];
        ElementNode nextStatic = TeleportBlockElement(
            "static",
            "must-be-carried",
            null,
            RenderPlan.None);
        ElementNode nextDynamic = TeleportBlockElement(
            "dynamic",
            "dynamic",
            "two",
            new RenderPlan(PatchFlags.Class));

        renderer.Render(
            new TeleportNode(
                "#deferred-block",
                [nextStatic, nextDynamic],
                isDeferred: true,
                renderPlan: new RenderPlan(
                    PatchFlags.NeedPatch,
                    dynamicChildren: [nextDynamic])),
            host.Container);
        host.RunScheduledFlushes();

        RendererParityNode[] updatedElements = ElementChildren(target);
        updatedElements[0].ShouldBeSameAs(staticHost);
        updatedElements[0].DescendantText.ShouldBe("compiler-static");
        updatedElements[1].ShouldBeSameAs(dynamicHost);
        updatedElements[1].Bindings["class"].ShouldBe("two");
    }

    [Fact]
    public void Teleport_SharedTargetKeyedReorder_PreservesTargetRangesAndPatchesIndependently()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#shared");

        renderer.Render(
            KeyedTeleportTree(
                ("a", "A"),
                ("b", "B")),
            host.Container);

        RendererParityNode[] initialText = TextChildren(target).ToArray();

        renderer.Render(
            KeyedTeleportTree(
                ("b", "B2"),
                ("a", "A")),
            host.Container);

        RendererParityNode[] reorderedText = TextChildren(target).ToArray();
        reorderedText[0].ShouldBeSameAs(initialText[1]);
        reorderedText[1].ShouldBeSameAs(initialText[0]);
        reorderedText.Select(node => node.Text).ShouldBe(["B2", "A"]);
        target.Children.Select(node => node.Text).ShouldBe(
        [
            "B2",
            "teleport anchor",
            "A",
            "teleport anchor",
        ]);
    }

    [Fact]
    public void Teleport_SharedTargetRotation_MovesTheTrailingLogicalRangeToTheTargetEnd()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#rotation");
        renderer.Render(
            KeyedTeleportTreeForTarget(
                "#rotation",
                ("a", "A"),
                ("b", "B"),
                ("c", "C")),
            host.Container);
        RendererParityNode[] initialText = TextChildren(target).ToArray();

        renderer.Render(
            KeyedTeleportTreeForTarget(
                "#rotation",
                ("b", "B"),
                ("c", "C"),
                ("a", "A")),
            host.Container);

        RendererParityNode[] rotated = TextChildren(target).ToArray();
        rotated.ShouldBe([initialText[1], initialText[2], initialText[0]]);
        rotated.Select(node => node.Text).ShouldBe(["B", "C", "A"]);
    }

    [Fact]
    public void Teleport_SharedTargetInsertion_PlacesNewRangeAtItsLogicalPosition()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#insertion");
        renderer.Render(
            KeyedTeleportTreeForTarget(
                "#insertion",
                ("a", "A"),
                ("b", "B")),
            host.Container);
        RendererParityNode[] initial = TextChildren(target).ToArray();

        renderer.Render(
            KeyedTeleportTreeForTarget(
                "#insertion",
                ("x", "X"),
                ("a", "A"),
                ("b", "B")),
            host.Container);

        RendererParityNode[] updated = TextChildren(target).ToArray();
        updated.Select(node => node.Text).ShouldBe(["X", "A", "B"]);
        updated[1].ShouldBeSameAs(initial[0]);
        updated[2].ShouldBeSameAs(initial[1]);
    }

    [Fact]
    public void Teleport_RetargetedRange_JoinsSharedTargetAtItsLogicalPosition()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode source = host.CreateTeleportTarget("#source");
        RendererParityNode target = host.CreateTeleportTarget("#retarget");
        renderer.Render(
            KeyedTeleportTree(
                ("x", "#source", "X", false),
                ("a", "#retarget", "A", false),
                ("b", "#retarget", "B", false)),
            host.Container);
        RendererParityNode moved = TextChildren(source).ShouldHaveSingleItem();
        RendererParityNode[] retained = TextChildren(target).ToArray();

        renderer.Render(
            KeyedTeleportTree(
                ("x", "#retarget", "X", false),
                ("a", "#retarget", "A", false),
                ("b", "#retarget", "B", false)),
            host.Container);

        source.Children.ShouldBeEmpty();
        RendererParityNode[] updated = TextChildren(target).ToArray();
        updated.ShouldBe([moved, retained[0], retained[1]]);
        updated.Select(node => node.Text).ShouldBe(["X", "A", "B"]);
    }

    [Fact]
    public void Teleport_MixedImmediateAndDeferredRanges_FollowLogicalTargetOrder()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#mixed");

        renderer.Render(
            KeyedTeleportTree(
                ("x", "#mixed", "X", true),
                ("a", "#mixed", "A", false),
                ("b", "#mixed", "B", true)),
            host.Container);

        host.RunScheduledFlushes();
        TextChildren(target).Select(node => node.Text).ShouldBe(["X", "A", "B"]);
    }

    [Fact]
    public void Teleport_Unmount_RemovesTargetRangeAndRunsComponentLifecycle()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RendererParityNode target = host.CreateTeleportTarget("#lifecycle");
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Teleported", instances, events);
        TeleportNode initial = new(
            "#lifecycle",
            children:
            [
                new ComponentNode(ComponentReference.ForName("Teleported")),
            ]);
        ApplicationContext application = CreateApplication(initial, components);
        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();

        renderer.Render(null, host.Container, application);
        host.RunScheduledFlushes();

        target.Children.ShouldBeEmpty();
        events.Count(value => value == "Teleported:unmounted").ShouldBe(1);
        instances.ShouldHaveSingleItem();
    }

    [Fact]
    public void KeepAlive_IncludeExclude_PreservesIncludedMountInDetachedStorage()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        RegisterKeepAliveProbe(components, "Beta", instances, events);
        KeepAliveNode initial = KeepAlive(
            "Alpha",
            ("include", "Alpha,Beta"),
            ("exclude", "Beta"));
        ApplicationContext application = CreateApplication(initial, components);

        renderer.Render(initial, host.Container, application);
        MountedComponentView<RendererParityNode> alphaView = FindView(renderer, host, "Alpha");
        RendererParityNode alphaHostNode = alphaView.FirstHostNode.ShouldNotBeNull();

        renderer.Render(
            KeepAlive(
                "Beta",
                ("include", "Alpha,Beta"),
                ("exclude", "Beta")),
            host.Container,
            application);

        alphaView.IsMounted.ShouldBeTrue();
        alphaHostNode.Parent.ShouldNotBeNull().Description.ShouldBe("storage");
        alphaHostNode.Parent!.Parent.ShouldBeNull();
        FindView(renderer, host, "Beta").FirstHostNode.ShouldNotBeNull()
            .Parent.ShouldBeSameAs(host.Container);

        renderer.Render(
            KeepAlive(
                "Alpha",
                ("include", "Alpha,Beta"),
                ("exclude", "Beta")),
            host.Container,
            application);

        instances.Count(instance => instance.Name == "Alpha").ShouldBe(1);
        instances.Count(instance => instance.Name == "Beta").ShouldBe(1);
        alphaView.IsMounted.ShouldBeTrue();
        alphaHostNode.Parent.ShouldBeSameAs(host.Container);
        events.Count(value => value == "Alpha:activated").ShouldBe(2);
        events.Count(value => value == "Alpha:deactivated").ShouldBe(1);
        events.Count(value => value == "Beta:unmounted").ShouldBe(1);
        events.ShouldNotContain("Beta:activated");
        renderer.GetMountedComponentViews(host.Container)
            .Count(view => ((KeepAliveProbeComponent)view.Instance).Name == "Beta")
            .ShouldBe(0);
    }

    [Fact]
    public void KeepAlive_PositiveMaximum_EvictsLeastRecentlyUsedMount()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        RegisterKeepAliveProbe(components, "Beta", instances, events);
        KeepAliveNode initial = KeepAlive("Alpha", ("maximum", 1));
        ApplicationContext application = CreateApplication(initial, components);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        MountedComponentView<RendererParityNode> firstAlpha = FindView(
            renderer,
            host,
            "Alpha");

        renderer.Render(
            KeepAlive("Beta", ("maximum", 1)),
            host.Container,
            application);
        host.RunScheduledFlushes();

        firstAlpha.IsMounted.ShouldBeFalse();
        events.Count(value => value == "Alpha:deactivated").ShouldBe(1);
        events.Count(value => value == "Alpha:unmounted").ShouldBe(1);
        events.IndexOf("Alpha:deactivated")
            .ShouldBeLessThan(events.IndexOf("Alpha:unmounted"));

        renderer.Render(
            KeepAlive("Alpha", ("maximum", 1)),
            host.Container,
            application);
        host.RunScheduledFlushes();

        instances.Count(instance => instance.Name == "Alpha").ShouldBe(2);
        FindView(renderer, host, "Alpha").Instance.ShouldNotBeSameAs(firstAlpha.Instance);
        events.Count(value => value == "Beta:unmounted").ShouldBe(1);
    }

    [Fact]
    public void KeepAlive_SameKeyDifferentComponent_UnmountsIncompatibleCachedEntry()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        RegisterKeepAliveProbe(components, "Beta", instances, events);
        KeepAliveNode initial = KeepAliveWithComponentKey("Alpha", "shared");
        ApplicationContext application = CreateApplication(initial, components);
        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        MountedComponentView<RendererParityNode> alpha = FindView(
            renderer,
            host,
            "Alpha");

        renderer.Render(
            KeepAliveWithComponentKey("Beta", "shared"),
            host.Container,
            application);
        host.RunScheduledFlushes();

        alpha.IsMounted.ShouldBeFalse();
        events.Count(value => value == "Alpha:deactivated").ShouldBe(1);
        events.Count(value => value == "Alpha:unmounted").ShouldBe(1);
        renderer.GetMountedComponentViews(host.Container)
            .Select(view => ((KeepAliveProbeComponent)view.Instance).Name)
            .ShouldBe(["Beta"]);

        renderer.Render(null, host.Container, application);
        host.RunScheduledFlushes();
        events.Count(value => value == "Alpha:unmounted").ShouldBe(1);
    }

    [Fact]
    public void KeepAlive_PredicateFilter_CachesOnlyMatchingComponentNames()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        RegisterKeepAliveProbe(components, "Beta", instances, events);
        Func<string, bool> include = name => string.Equals(
            name,
            "Alpha",
            StringComparison.Ordinal);
        KeepAliveNode initial = KeepAlive("Alpha", ("include", include));
        ApplicationContext application = CreateApplication(initial, components);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        renderer.Render(
            KeepAlive("Beta", ("include", include)),
            host.Container,
            application);
        host.RunScheduledFlushes();
        renderer.Render(
            KeepAlive("Alpha", ("include", include)),
            host.Container,
            application);
        host.RunScheduledFlushes();

        instances.Count(instance => instance.Name == "Alpha").ShouldBe(1);
        instances.Count(instance => instance.Name == "Beta").ShouldBe(1);
        events.ShouldNotContain("Beta:activated");
        events.Count(value => value == "Beta:unmounted").ShouldBe(1);
    }

    [Fact]
    public void KeepAlive_ChangedFilter_PrunesNewlyExcludedCachedComponent()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        RegisterKeepAliveProbe(components, "Beta", instances, events);
        KeepAliveNode initial = KeepAlive("Alpha");
        ApplicationContext application = CreateApplication(initial, components);
        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        renderer.Render(KeepAlive("Beta"), host.Container, application);
        host.RunScheduledFlushes();

        renderer.Render(
            KeepAlive("Beta", ("include", "Beta")),
            host.Container,
            application);
        host.RunScheduledFlushes();

        events.Count(value => value == "Alpha:unmounted").ShouldBe(1);
        renderer.Render(KeepAlive("Alpha"), host.Container, application);
        host.RunScheduledFlushes();
        instances.Count(instance => instance.Name == "Alpha").ShouldBe(2);
    }

    [Fact]
    public void KeepAlive_NestedStructuralWrappers_InvokeLifecycleChildBeforeParent()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<string> events = [];
        RegisterNestedKeepAliveProbe(
            components,
            "Outer",
            "outer",
            events,
            NestedKeepAliveWrappers);
        RegisterNestedKeepAliveProbe(
            components,
            "Inner",
            "inner",
            events,
            () => new ElementNode(new QualifiedName("inner")));
        RegisterNestedKeepAliveProbe(
            components,
            "Alternative",
            "alternative",
            events,
            () => new ElementNode(new QualifiedName("alternative")));
        KeepAliveNode initial = KeepAlive("Outer");
        ApplicationContext application = CreateApplication(initial, components);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();

        events.Where(IsInnerOrOuterLifecycle).ShouldBe(
        [
            "inner:activated",
            "outer:activated",
        ]);

        events.Clear();
        renderer.Render(KeepAlive("Alternative"), host.Container, application);
        host.RunScheduledFlushes();

        events.Where(IsInnerOrOuterLifecycle).ShouldBe(
        [
            "inner:deactivated",
            "outer:deactivated",
        ]);

        events.Clear();
        renderer.Render(KeepAlive("Outer"), host.Container, application);
        host.RunScheduledFlushes();

        events.Where(IsInnerOrOuterLifecycle).ShouldBe(
        [
            "inner:activated",
            "outer:activated",
        ]);
    }

    [Fact]
    public void KeepAlive_RootUnmount_DeactivatesCachedTreeBeforeUnmountingIt()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ComponentFactory components = new();
        List<KeepAliveProbeComponent> instances = [];
        List<string> events = [];
        RegisterKeepAliveProbe(components, "Alpha", instances, events);
        KeepAliveNode initial = KeepAlive("Alpha");
        ApplicationContext application = CreateApplication(initial, components);
        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        events.Clear();

        renderer.Render(null, host.Container, application);
        host.RunScheduledFlushes();

        events.ShouldBe(
        [
            "Alpha:deactivated",
            "Alpha:unmounted",
        ]);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public async Task Suspense_PendingAsynchronousContent_ShowsFallbackThenRevealsContent()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<SuspenseWrapperIdentityComponent>(
                _ => load.Task);
        ComponentFactory components = new();
        components.Register(definition.Registration);
        ComponentReference targetReference = ComponentReference.ForType(
            typeof(SuspenseResolvedComponent));
        components.Register(
            new ComponentRegistration(
                targetReference,
                new ComponentContract(),
                _ => new SuspenseResolvedComponent()));
        SuspenseNode suspense = new(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => definition.CreateComponent(),
                    ["fallback"] = _ => new TextNode("waiting"),
                }));
        ApplicationContext application = CreateApplication(suspense, components);

        renderer.Render(suspense, host.Container, application);

        RendererParityNode fallback = TextChildren(host.Container).ShouldHaveSingleItem();
        fallback.Text.ShouldBe("waiting");

        load.SetResult(new AsynchronousComponentTarget(targetReference));
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        fallback.Parent.ShouldBeNull();
        TextChildren(host.Container).ShouldHaveSingleItem().Text.ShouldBe("resolved");
    }

    [Fact]
    public void Transition_MountPatchAndUnmount_AreTransparentToTheHostTree()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        TransitionNode initial = Transition("before");

        renderer.Render(initial, host.Container);

        RendererParityNode hostText = host.Container.Children.ShouldHaveSingleItem();
        hostText.Kind.ShouldBe(RendererParityNodeKind.Text);
        hostText.Text.ShouldBe("before");

        renderer.Render(Transition("after"), host.Container);

        RendererParityNode patched = host.Container.Children.ShouldHaveSingleItem();
        patched.ShouldBeSameAs(hostText);
        patched.Text.ShouldBe("after");
        renderer.GetMountedComponentViews(host.Container).ShouldBeEmpty();

        renderer.Render(null, host.Container);

        host.Container.Children.ShouldBeEmpty();
        hostText.Parent.ShouldBeNull();
    }

    private static IReadOnlyList<RendererParityNode> TextChildren(
        RendererParityNode root)
    {
        List<RendererParityNode> result = [];
        AddTextChildren(root, result);
        return result;
    }

    private static void AddTextChildren(
        RendererParityNode node,
        List<RendererParityNode> result)
    {
        if (node.Kind == RendererParityNodeKind.Text)
        {
            result.Add(node);
            return;
        }

        for (int index = 0; index < node.Children.Count; index++)
        {
            AddTextChildren(node.Children[index], result);
        }
    }

    private static RendererParityNode[] ElementChildren(RendererParityNode root)
    {
        return root.Children
            .Where(node => node.Kind == RendererParityNodeKind.Element)
            .ToArray();
    }

    private static ElementNode TeleportBlockElement(
        string name,
        string text,
        string? className,
        RenderPlan renderPlan)
    {
        ElementBinding[] bindings = className is null
            ? []
            : [ElementBinding.Attribute(new QualifiedName("class"), className)];
        return new ElementNode(
            new QualifiedName(name),
            bindings,
            [new TextNode(text)],
            renderPlan: renderPlan);
    }

    private static FragmentNode KeyedTeleportTree(
        params (string Key, string Text)[] values)
    {
        return KeyedTeleportTreeForTarget("#shared", values);
    }

    private static FragmentNode KeyedTeleportTreeForTarget(
        string target,
        params (string Key, string Text)[] values)
    {
        List<VirtualNode> teleports = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            (string key, string text) = values[index];
            teleports.Add(
                new TeleportNode(
                    target,
                    [new TextNode(text)],
                    key: key));
        }

        return new FragmentNode(
            teleports,
            renderPlan: new RenderPlan(PatchFlags.KeyedFragment));
    }

    private static FragmentNode KeyedTeleportTree(
        params (string Key, string Target, string Text, bool IsDeferred)[] values)
    {
        List<VirtualNode> teleports = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            (string key, string target, string text, bool isDeferred) = values[index];
            teleports.Add(
                new TeleportNode(
                    target,
                    [new TextNode(text)],
                    isDeferred: isDeferred,
                    key: key));
        }

        return new FragmentNode(
            teleports,
            renderPlan: new RenderPlan(PatchFlags.KeyedFragment));
    }

    private static KeepAliveNode KeepAlive(
        string componentName,
        params (string Name, object? Value)[] arguments)
    {
        Dictionary<string, object?> argumentValues = new(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index++)
        {
            argumentValues.Add(arguments[index].Name, arguments[index].Value);
        }

        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["default"] = _ => new ComponentNode(
                ComponentReference.ForName(componentName),
                key: componentName),
        };
        return new KeepAliveNode(new ComponentInvocation(argumentValues, slots));
    }

    private static KeepAliveNode KeepAliveWithComponentKey(
        string componentName,
        object componentKey)
    {
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["default"] = _ => new ComponentNode(
                ComponentReference.ForName(componentName),
                key: componentKey),
        };
        return new KeepAliveNode(new ComponentInvocation(slots: slots));
    }

    private static TransitionNode Transition(string text)
    {
        return new TransitionNode(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => new TextNode(text),
                }));
    }

    private static VirtualNode NestedKeepAliveWrappers()
    {
        KeepAliveNode keepAlive = KeepAlive("Inner");
        SuspenseNode suspense = new(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => keepAlive,
                    ["fallback"] = _ => new CommentNode("fallback"),
                }));
        return new TransitionNode(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => suspense,
                }));
    }

    private static bool IsInnerOrOuterLifecycle(string value) =>
        value.StartsWith("inner:", StringComparison.Ordinal)
        || value.StartsWith("outer:", StringComparison.Ordinal);

    private static void RegisterKeepAliveProbe(
        ComponentFactory components,
        string name,
        List<KeepAliveProbeComponent> instances,
        List<string> events)
    {
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForName(name),
                new ComponentContract(displayName: name),
                _ =>
                {
                    KeepAliveProbeComponent instance = new(name, events);
                    instances.Add(instance);
                    return instance;
                }));
    }

    private static void RegisterNestedKeepAliveProbe(
        ComponentFactory components,
        string registeredName,
        string eventName,
        List<string> events,
        Func<VirtualNode> render)
    {
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForName(registeredName),
                new ComponentContract(displayName: registeredName),
                _ => new NestedKeepAliveProbeComponent(eventName, events, render)));
    }

    private static MountedComponentView<RendererParityNode> FindView(
        Renderer<RendererParityNode> renderer,
        RendererParityHost host,
        string name)
    {
        return renderer.GetMountedComponentViews(host.Container)
            .Single(view => ((KeepAliveProbeComponent)view.Instance).Name == name);
    }

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        ComponentFactory components)
    {
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
    }

    private static async Task WaitForPendingSchedulerFlushAsync()
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            if (Scheduler.IsFlushPending)
            {
                return;
            }

            await Task.Delay(1);
        }

        throw new InvalidOperationException(
            "The asynchronous component did not schedule renderer work.");
    }

    private sealed class KeepAliveProbeComponent : IComponent
    {
        private readonly List<string> _events;

        internal KeepAliveProbeComponent(string name, List<string> events)
        {
            Name = name;
            _events = events;
        }

        internal string Name { get; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnMounted(() => _events.Add($"{Name}:mounted"));
            context.Lifecycle.OnActivated(() => _events.Add($"{Name}:activated"));
            context.Lifecycle.OnDeactivated(() => _events.Add($"{Name}:deactivated"));
            context.Lifecycle.OnUnmounted(() => _events.Add($"{Name}:unmounted"));
            return _ => new ElementNode(
                new QualifiedName("probe"),
                children: [new TextNode(Name)]);
        }
    }

    private sealed class NestedKeepAliveProbeComponent : IComponent
    {
        private readonly string _eventName;
        private readonly List<string> _events;
        private readonly Func<VirtualNode> _render;

        internal NestedKeepAliveProbeComponent(
            string eventName,
            List<string> events,
            Func<VirtualNode> render)
        {
            _eventName = eventName;
            _events = events;
            _render = render;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnActivated(
                () => _events.Add($"{_eventName}:activated"));
            context.Lifecycle.OnDeactivated(
                () => _events.Add($"{_eventName}:deactivated"));
            return _ => _render();
        }
    }

    private sealed class SuspenseWrapperIdentityComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class SuspenseResolvedComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode("resolved");
    }
}
