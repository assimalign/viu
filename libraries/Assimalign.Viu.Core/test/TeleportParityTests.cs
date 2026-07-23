using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins Teleport's deferred mounting and block patching to Vue 3.5's runtime-core behavior.
/// </summary>
public sealed class TeleportParityTests : IDisposable
{
    public TeleportParityTests()
    {
        Scheduler.Reset();
        RenderHelpers.ResetBlockTrackingForTests();
        Renderer<FakeHostNode>.PatchVisitCount = 0;
    }

    public void Dispose()
    {
        Scheduler.Reset();
        RenderHelpers.ResetBlockTrackingForTests();
    }

    [Fact]
    public void Render_DeferredTeleport_ResolvesTargetRenderedLaterInSameTree()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IComponent teleport = RenderHelpers._createVNode(
            RenderHelpers._Teleport,
            RenderHelpers._createProps(
                ("to", "#late"),
                ("defer", true)),
            new object?[]
            {
                RenderHelpers._createElementVNode(
                    "span",
                    null,
                    "deferred"),
            });
        IComponent tree = ComponentTree.Fragment(
        [
            teleport,
            ElementWithIdentifier("div", "late"),
        ]);
        List<string> warnings = [];
        IApplicationContext application =
            CreateApplication(tree, warnings);

        renderer.Render(tree, host.Root, application);
        pump.RunUntilIdle();

        ITeleportComponent teleportComponent =
            teleport.ShouldBeAssignableTo<ITeleportComponent>();
        teleportComponent.IsDeferred.ShouldBeTrue();
        FakeHostNode target = Elements(host.Root).Single();
        host.Text(target).ShouldBe("deferred");
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Render_NonDeferredTeleport_DoesNotRetryTargetRenderedLaterInSameTree()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IComponent tree = ComponentTree.Fragment(
        [
            ComponentTree.Teleport(
                "#late",
                [ComponentTree.Element("span")]),
            ElementWithIdentifier("div", "late"),
        ]);
        List<string> warnings = [];
        IApplicationContext application =
            CreateApplication(tree, warnings);

        renderer.Render(tree, host.Root, application);

        FakeHostNode target = Elements(host.Root).Single();
        Elements(target).ShouldBeEmpty();
        warnings.ShouldContain(
            warning => warning.Contains(
                "Failed to resolve teleport target",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Render_DisabledDeferredTeleport_MountsLogicallyBeforeTargetSetup()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        bool mountedLogicallyBeforePostFlush = false;
        bool targetWasEmptyBeforePostFlush = false;

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        ComponentTree.Teleport(
                            target,
                            [
                                ComponentTree.Element(
                                    "span",
                                    children:
                                    [
                                        ComponentTree.Text("local"),
                                    ]),
                            ],
                            isDisabled: true,
                            isDeferred: true),
                        host.Root);
                    mountedLogicallyBeforePostFlush =
                        Elements(host.Root).Length == 1;
                    targetWasEmptyBeforePostFlush =
                        Elements(target).Length == 0;
                }));

        pump.RunUntilIdle();

        mountedLogicallyBeforePostFlush.ShouldBeTrue();
        targetWasEmptyBeforePostFlush.ShouldBeTrue();
        host.Text(Elements(host.Root).Single()).ShouldBe("local");
        Elements(target).ShouldBeEmpty();
    }

    [Fact]
    public void Render_PendingDeferredUpdate_AppliesOnlyLatestTreeAfterPostFlush()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        bool targetWasEmptyBeforePostFlush = false;

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        DeferredTeleport(target, "stale"),
                        host.Root);
                    renderer.Render(
                        DeferredTeleport(target, "latest"),
                        host.Root);
                    targetWasEmptyBeforePostFlush =
                        Elements(target).Length == 0;
                }));

        pump.RunUntilIdle();

        targetWasEmptyBeforePostFlush.ShouldBeTrue();
        FakeHostNode child = Elements(target).Single();
        host.Text(child).ShouldBe("latest");
    }

    [Fact]
    public void Render_UnmountPendingDeferredTeleport_CancelsTargetMount()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    renderer.Render(
                        DeferredTeleport(target, "stale"),
                        host.Root);
                    renderer.Render(null, host.Root);
                }));

        pump.RunUntilIdle();

        host.Root.Children.ShouldBeEmpty();
        target.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Patch_BlockTeleport_VisitsDynamicChildAndCarriesStaticHostNode()
    {
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponent Build(string staticText, string dynamicClass, bool disabled)
        {
            BlockToken block = RenderHelpers._openBlock();
            return RenderHelpers._createBlock(
                block,
                RenderHelpers._Teleport,
                RenderHelpers._createProps(
                    ("to", target),
                    ("disabled", disabled)),
                new object?[]
                {
                    RenderHelpers._createElementVNode(
                        "div",
                        null,
                        staticText),
                    RenderHelpers._createElementVNode(
                        "span",
                        RenderHelpers._createProps(
                            ("class", dynamicClass)),
                        "dynamic",
                        (int)PatchFlags.Class),
                });
        }

        renderer.Render(Build("static", "one", disabled: false), host.Root);
        FakeHostNode staticElement = Elements(target)[0];
        Renderer<FakeHostNode>.PatchVisitCount = 0;

        renderer.Render(Build("changed", "two", disabled: false), host.Root);

        Renderer<FakeHostNode>.PatchVisitCount.ShouldBe(2);
        FakeHostNode[] targetElements = Elements(target);
        targetElements[0].ShouldBeSameAs(staticElement);
        host.Text(targetElements[0]).ShouldBe("static");
        targetElements[1].Attributes["class"].ShouldBe("two");

        renderer.Render(Build("changed-again", "three", disabled: true), host.Root);

        FakeHostNode[] logicalElements = Elements(host.Root);
        logicalElements[0].ShouldBeSameAs(staticElement);
        host.Text(logicalElements[0]).ShouldBe("static");
        logicalElements[1].Attributes["class"].ShouldBe("three");
    }

    [Fact]
    public void Patch_MultipleTeleportsSharingTarget_PreserveMountOrderAndPatchIndependently()
    {
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponent Build(string first, string second)
        {
            return ComponentTree.Fragment(
            [
                ComponentTree.Teleport(
                    target,
                    [
                        ComponentTree.Element(
                            "span",
                            children: [ComponentTree.Text(first)]),
                    ]),
                ComponentTree.Teleport(
                    target,
                    [
                        ComponentTree.Element(
                            "span",
                            children: [ComponentTree.Text(second)]),
                    ]),
            ]);
        }

        renderer.Render(Build("A", "B"), host.Root);
        FakeHostNode[] initial = Elements(target);
        renderer.Render(Build("A", "B2"), host.Root);

        FakeHostNode[] updated = Elements(target);
        updated[0].ShouldBeSameAs(initial[0]);
        updated[1].ShouldBeSameAs(initial[1]);
        host.Text(updated[0]).ShouldBe("A");
        host.Text(updated[1]).ShouldBe("B2");
    }

    [Fact]
    public void Patch_KeyedTeleportReorder_LeavesEnabledContentInTargetOrder()
    {
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponent Keyed(string key)
        {
            return ComponentTree.Teleport(
                target,
                [
                    ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text(key)]),
                ],
                key: key);
        }

        IComponent Build(string first, string second)
        {
            return ComponentTree.Fragment(
                [Keyed(first), Keyed(second)],
                optimization: new ComponentOptimization(
                    PatchFlags.KeyedFragment));
        }

        renderer.Render(Build("a", "b"), host.Root);
        FakeHostNode[] initial = Elements(target);
        renderer.Render(Build("b", "a"), host.Root);

        FakeHostNode[] updated = Elements(target);
        updated.ShouldBe(initial);
        host.Text(updated[0]).ShouldBe("a");
        host.Text(updated[1]).ShouldBe("b");
    }

    [Fact]
    public void Unmount_TeleportedTemplate_RemovesTargetAndRunsLifecycle()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> events = [];
        FakeHost host = new();
        FakeHostNode target = host.CreateContainer("target");
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        UnmountTemplate template = new(events);
        IComponent root = ComponentTree.Teleport(
            target,
            [ComponentTree.Template<UnmountTemplate>()]);
        IApplicationContext application = new ApplicationContext(
            root,
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(UnmountTemplate),
                    () => template),
            ]),
            new EmptyServiceProvider());
        renderer.Render(root, host.Root, application);

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        events.ShouldBe(["unmounted"]);
        target.Children.ShouldBeEmpty();
        host.Root.Children.ShouldBeEmpty();
    }

    private static IElementComponent ElementWithIdentifier(
        string tag,
        string identifier)
    {
        return ComponentTree.Element(
            tag,
            new ComponentAttributes(
            [
                new ComponentAttribute("id", identifier),
            ]));
    }

    private static FakeHostNode[] Elements(FakeHostNode root)
    {
        return root.Children
            .Where(node => node.Kind == FakeHostNodeKind.Element)
            .ToArray();
    }

    private static IApplicationContext CreateApplication(
        IComponent root,
        List<string> warnings)
    {
        ApplicationContext application = new(
            root,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider());
        application.WarnHandler = warnings.Add;
        return application;
    }

    private static ITeleportComponent DeferredTeleport(
        FakeHostNode target,
        string text)
    {
        return ComponentTree.Teleport(
            target,
            [
                ComponentTree.Element(
                    "span",
                    children: [ComponentTree.Text(text)]),
            ],
            isDeferred: true);
    }

    private sealed class UnmountTemplate : IComponentTemplate
    {
        private readonly List<string> _events;

        internal UnmountTemplate(List<string> events)
        {
            _events = events;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnUnmounted(
                () => _events.Add("unmounted"));
            return static () => ComponentTree.Element("span");
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }
}
