using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins the per-render flush budget specified by [RND-HOST-4], [RND-IO-1], and [SCH-10..11].
public sealed class BrowserRendererHostTests
{
    private const int ContainerHandle = 100;
    private const int TeleportTargetHandle = 200;
    private const string TeleportTargetSelector = "#overlay";

    [Fact]
    public void Render_SynchronousFlush_AppliesOneNonemptyCommandFrame()
    {
        List<int> frameLengths = [];
        var host = new BrowserRendererHost(
            (_, length) =>
            {
                frameLengths.Add(length);
                return [];
            });
        host.ObserveForeignHandle(100);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        var initial = new ElementNode(
            new QualifiedName("div"),
            [ElementBinding.Attribute(new QualifiedName("id"), "root")],
            [new TextNode("first")]);

        renderer.Render(initial, 100);

        host.InteropCallCount.ShouldBe(1);
        frameLengths.Count.ShouldBe(1);
        frameLengths[0].ShouldBeGreaterThan(0);

        var updated = new ElementNode(
            new QualifiedName("div"),
            [ElementBinding.Attribute(new QualifiedName("id"), "root")],
            [new TextNode("second")]);
        renderer.Render(updated, 100);

        host.InteropCallCount.ShouldBe(2);
        frameLengths.Count.ShouldBe(2);

        renderer.Render(null, 100);

        host.InteropCallCount.ShouldBe(3);
        frameLengths.Count.ShouldBe(3);
    }

    [Fact]
    public void Commit_EmptyFrame_IsIdempotent()
    {
        int applyCount = 0;
        var host = new BrowserRendererHost(
            (_, _) =>
            {
                applyCount++;
                return [];
            });

        host.Options.Commit!();
        host.Options.Commit!();

        applyCount.ShouldBe(0);
        host.InteropCallCount.ShouldBe(0);
    }

    [Fact]
    public void Options_ExplicitHydrationTriggerSeam_ForwardsTheMarkerBoundedRequest()
    {
        HydrationTriggerRequest<int>? received = null;
        var registration = new TrackingHydrationTriggerRegistration();
        var host = new BrowserRendererHost(
            (_, _) => [],
            parentNode: null,
            nextSibling: null,
            snapshotHydration: null,
            scheduleHydrationTrigger: request =>
            {
                received = request;
                return registration;
            });
        HydrationTriggerRequest<int> request = new(
            HydrationStrategy.OnIdle(25),
            startAnchor: 7,
            endAnchor: 11,
            trigger: static () => { });

        IHydrationTriggerRegistration actual =
            host.Options.ScheduleHydrationTrigger!(request);

        received.ShouldBeSameAs(request);
        actual.ShouldBeSameAs(registration);
    }

    [Fact]
    public void Activate_SecondHost_RejectsUntilFirstLeaseIsDisposed()
    {
        var firstHost = new BrowserRendererHost((_, _) => []);
        var secondHost = new BrowserRendererHost((_, _) => []);
        IDisposable firstActivation = firstHost.Activate();

        Action activateSecond = () => secondHost.Activate().Dispose();

        activateSecond.ShouldThrow<InvalidOperationException>();
        firstActivation.Dispose();
        firstActivation.Dispose();

        using IDisposable secondActivation = secondHost.Activate();
    }

    [Fact]
    public void Render_ScheduledFlush_CoalescesMultipleMutationsIntoOneCommandFrame()
    {
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(200);
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            renderer.Render(new TextNode("initial"), 200);
            host.InteropCallCount.ShouldBe(1);

            var updateJob = new SchedulerJob(
                () =>
                {
                    renderer.Render(new TextNode("intermediate"), 200);
                    renderer.Render(new TextNode("final"), 200);
                });
            Scheduler.QueueJob(updateJob);

            host.InteropCallCount.ShouldBe(1);
            scheduledFlush.ShouldNotBeNull();
            scheduledFlush();

            host.InteropCallCount.ShouldBe(2);
            Scheduler.IsFlushPending.ShouldBeFalse();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Render_BlockTransitionSynchronousLeaveMountsComponentWithKeepAliveAndRemainsPatchable()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            scheduledFlushes.Enqueue);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(ContainerHandle);
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ComponentFactory components = CreateStorageComponentFactory(
                includeDeferredTeleport: false);
            ElementNode initial = TransitionBlock(OutgoingElement());
            ApplicationContext application = CreateApplication(initial, components);
            renderer.Render(initial, ContainerHandle, application);
            RunScheduledFlushes(scheduledFlushes);

            renderer.Render(
                TransitionBlock(IncomingComponent()),
                ContainerHandle);
            RunScheduledFlushes(scheduledFlushes);

            MountedComponentView<int> incoming = renderer
                .GetMountedComponentViews(ContainerHandle)
                .Single(view => view.Instance is IncomingStorageComponent);
            renderer.GetMountedComponentViews(ContainerHandle)
                .ShouldContain(view => view.Instance is StorageLeafComponent);

            Should.NotThrow(
                () => renderer.Render(
                    TransitionBlock(ReplacementElement()),
                    ContainerHandle));
            RunScheduledFlushes(scheduledFlushes);
            incoming.IsMounted.ShouldBeFalse();
            Should.NotThrow(() => renderer.Render(null, ContainerHandle));
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Render_BlockTransitionIncomingComponentWithDeferredTeleportRemainsPatchable()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            scheduledFlushes.Enqueue);
        try
        {
            int teleportResolutionCount = 0;
            var operations = new BufferedBrowserNodeOperations(
                (_, _) => [],
                selector =>
                {
                    selector.ShouldBe(TeleportTargetSelector);
                    teleportResolutionCount++;
                    return TeleportTargetHandle;
                },
                static _ => 0,
                static _ => 0,
                insertStaticContent: null);
            operations.ObserveForeignHandle(ContainerHandle);
            operations.ObserveForeignHandle(TeleportTargetHandle);
            Renderer<int> renderer = RendererFactory.CreateRenderer(operations.Create());
            ComponentFactory components = CreateStorageComponentFactory(
                includeDeferredTeleport: true);
            ElementNode initial = TransitionBlock(OutgoingElement());
            ApplicationContext application = CreateApplication(initial, components);
            renderer.Render(initial, ContainerHandle, application);
            RunScheduledFlushes(scheduledFlushes);

            renderer.Render(
                TransitionBlock(IncomingComponent()),
                ContainerHandle);
            RunScheduledFlushes(scheduledFlushes);

            teleportResolutionCount.ShouldBe(1);
            MountedComponentView<int> incoming = renderer
                .GetMountedComponentViews(ContainerHandle)
                .Single(view => view.Instance is IncomingStorageComponent);
            renderer.GetMountedComponentViews(ContainerHandle)
                .ShouldContain(view => view.Instance is StorageLeafComponent);

            Should.NotThrow(
                () => renderer.Render(
                    TransitionBlock(ReplacementElement()),
                    ContainerHandle));
            RunScheduledFlushes(scheduledFlushes);
            incoming.IsMounted.ShouldBeFalse();
            Should.NotThrow(() => renderer.Render(null, ContainerHandle));
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Render_BlockTransitionSynchronousLeaveMountsTransitionGroupKeepAliveAndEagerTeleportWithoutErrors()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        List<Exception> errors = [];
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            scheduledFlushes.Enqueue);
        try
        {
            int teleportResolutionCount = 0;
            var operations = new BufferedBrowserNodeOperations(
                (_, _) => [],
                selector =>
                {
                    selector.ShouldBe(TeleportTargetSelector);
                    teleportResolutionCount++;
                    return TeleportTargetHandle;
                },
                static _ => 0,
                static _ => 0,
                insertStaticContent: null);
            operations.ObserveForeignHandle(ContainerHandle);
            operations.ObserveForeignHandle(TeleportTargetHandle);
            Renderer<int> renderer = RendererFactory.CreateRenderer(operations.Create());
            ComponentFactory components = CreateStorageComponentFactory(
                includeDeferredTeleport: false,
                includeTransitionGroup: true,
                includeEagerTeleport: true);
            ElementNode initial = TransitionBlock(OutgoingElement());
            ApplicationContext application = CreateApplication(
                initial,
                components,
                (exception, _, _) => errors.Add(exception));
            renderer.Render(initial, ContainerHandle, application);
            RunScheduledFlushes(scheduledFlushes);

            Should.NotThrow(
                () => renderer.Render(
                    TransitionBlock(IncomingComponent()),
                    ContainerHandle));
            RunScheduledFlushes(scheduledFlushes);

            teleportResolutionCount.ShouldBe(1);
            renderer.GetMountedComponentViews(ContainerHandle)
                .ShouldContain(view => view.Instance is IncomingStorageComponent);
            renderer.GetMountedComponentViews(ContainerHandle)
                .ShouldContain(view => view.Instance is TransitionGroup);
            renderer.GetMountedComponentViews(ContainerHandle)
                .ShouldContain(view => view.Instance is StorageLeafComponent);
            errors.ShouldBeEmpty();
            renderer.Render(null, ContainerHandle);
            RunScheduledFlushes(scheduledFlushes);
            errors.ShouldBeEmpty();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Render_BlockTransitionSynchronousLeaveInternalBrowserMountFailureEscapesErrorHandler()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        List<Exception> errors = [];
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            scheduledFlushes.Enqueue);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(ContainerHandle);
            host.ObserveForeignHandle(TeleportTargetHandle);
            Renderer<int> renderer = RendererFactory.CreateRenderer(
                new RendererOptions<int>
                {
                    Insert = host.Options.Insert,
                    Remove = host.Options.Remove,
                    CreateElement = host.Options.CreateElement,
                    CreateText = host.Options.CreateText,
                    CreateComment = host.Options.CreateComment,
                    SetText = host.Options.SetText,
                    ParentNode = host.Options.ParentNode,
                    NextSibling = host.Options.NextSibling,
                    PatchAttribute = host.Options.PatchAttribute,
                    ResolveTeleportTarget = _ => TeleportTargetHandle,
                    Commit = host.Options.Commit,
                    InsertStaticContent = host.Options.InsertStaticContent,
                    CreateHydrationReader = host.Options.CreateHydrationReader,
                });
            ComponentFactory components = CreateStorageComponentFactory(
                includeDeferredTeleport: false,
                includeTransitionGroup: true,
                includeEagerTeleport: true,
                includeInvalidNamespace: true);
            ElementNode initial = TransitionBlock(OutgoingElement());
            ApplicationContext application = CreateApplication(
                initial,
                components,
                (exception, _, _) => errors.Add(exception));
            renderer.Render(initial, ContainerHandle, application);
            RunScheduledFlushes(scheduledFlushes);

            Action replace = () => renderer.Render(
                TransitionBlock(IncomingComponent()),
                ContainerHandle);

            NotSupportedException exception = replace.ShouldThrow<NotSupportedException>();
            exception.Message.ShouldBe(
                "The Browser host does not support element namespace 'urn:invalid'.");
            errors.ShouldBeEmpty();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Theory]
    [InlineData("div", null, null)]
    [InlineData("svg", null, "svg")]
    [InlineData("path", "http://www.w3.org/2000/svg", "svg")]
    [InlineData("foreignObject", null, "svg")]
    [InlineData("math", null, "mathml")]
    [InlineData("mi", "http://www.w3.org/1998/Math/MathML", "mathml")]
    [InlineData("storage", "urn:assimalign:viu:internal", null)]
    public void CreateElement_QualifiedName_EncodesHostOwnedNamespace(
        string localName,
        string? namespaceName,
        string? expectedNamespace)
    {
        byte[]? appliedFrame = null;
        var host = new BrowserRendererHost(
            (frame, length) =>
            {
                appliedFrame = frame.AsSpan(0, length).ToArray();
                return [];
            });

        _ = host.Options.CreateElement(
            new QualifiedName(localName, namespaceName));
        host.Options.Commit!();

        byte[] frame = appliedFrame
            ?? throw new InvalidOperationException("The command frame was not applied.");
        frame[14].ShouldBe((byte)1);
        ReadCreateElementNamespace(frame).ShouldBe(expectedNamespace);
    }

    [Fact]
    public void CreateElement_UnknownNamespace_RejectsBeforeCommit()
    {
        var host = new BrowserRendererHost((_, _) => []);

        Action create = () => host.Options.CreateElement(
            new QualifiedName("widget", "urn:unknown"));

        create.ShouldThrow<NotSupportedException>();
        host.Options.Commit!();
        host.InteropCallCount.ShouldBe(0);
    }

    private static string? ReadCreateElementNamespace(byte[] frame)
    {
        const int namespaceReferenceOffset = 23;
        int namespaceIndex = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(namespaceReferenceOffset, sizeof(int)));
        if (namespaceIndex < 0)
        {
            return null;
        }

        int cursor = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(10, sizeof(int)));
        int stringCount = ReadInt32(frame, ref cursor);
        for (int index = 0; index < stringCount; index++)
        {
            int byteCount = ReadInt32(frame, ref cursor);
            string value = Encoding.UTF8.GetString(
                frame.AsSpan(cursor, byteCount));
            cursor += byteCount;
            if (index == namespaceIndex)
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Namespace string index '{namespaceIndex}' is outside the command frame string table.");
    }

    private static int ReadInt32(byte[] frame, ref int cursor)
    {
        int value = BinaryPrimitives.ReadInt32LittleEndian(
            frame.AsSpan(cursor, sizeof(int)));
        cursor += sizeof(int);
        return value;
    }

    private static ComponentFactory CreateStorageComponentFactory(
        bool includeDeferredTeleport,
        bool includeTransitionGroup = false,
        bool includeEagerTeleport = false,
        bool includeInvalidNamespace = false)
    {
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(IncomingStorageComponent)),
                new ComponentContract(),
                _ => new IncomingStorageComponent(
                    includeDeferredTeleport,
                    includeTransitionGroup,
                    includeEagerTeleport,
                    includeInvalidNamespace)));
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(StorageLeafComponent)),
                new ComponentContract(),
                static _ => new StorageLeafComponent()));
        components.Register(TransitionGroup.Registration);
        return components;
    }

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        ComponentFactory components,
        Action<Exception, ComponentContext?, string>? errorHandler = null) =>
        new(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
                ErrorHandler = errorHandler,
            });

    private static ElementNode TransitionBlock(VirtualNode child)
    {
        TransitionNode transition = SynchronousOutgoingThenIncoming(child);
        return new ElementNode(
            new QualifiedName("block-root"),
            children: [transition],
            renderPlan: new RenderPlan(
                PatchFlags.NeedPatch,
                dynamicChildren: [transition]));
    }

    private static TransitionNode SynchronousOutgoingThenIncoming(VirtualNode child)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [TransitionProperties.ResolvedArgument] = new TransitionProperties
            {
                Mode = "out-in",
                OnEnter = static (_, complete) => complete(),
                OnLeave = static (_, complete) => complete(),
            },
        };
        var slots = new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
        {
            ["default"] = _ => child,
        };
        return new TransitionNode(new ComponentInvocation(arguments, slots));
    }

    private static ElementNode OutgoingElement() =>
        new(
            new QualifiedName("section"),
            children: [new TextNode("outgoing")],
            key: "outgoing");

    private static ComponentNode IncomingComponent() =>
        new(
            ComponentReference.ForType(typeof(IncomingStorageComponent)),
            key: "incoming");

    private static ElementNode ReplacementElement() =>
        new(
            new QualifiedName("section"),
            children: [new TextNode("replacement")],
            key: "replacement");

    private static void RunScheduledFlushes(Queue<Action> scheduledFlushes)
    {
        while (scheduledFlushes.Count > 0)
        {
            scheduledFlushes.Dequeue()();
        }
    }

    private sealed class IncomingStorageComponent : IComponent
    {
        private readonly bool _includeDeferredTeleport;
        private readonly bool _includeTransitionGroup;
        private readonly bool _includeEagerTeleport;
        private readonly bool _includeInvalidNamespace;

        internal IncomingStorageComponent(
            bool includeDeferredTeleport,
            bool includeTransitionGroup,
            bool includeEagerTeleport,
            bool includeInvalidNamespace)
        {
            _includeDeferredTeleport = includeDeferredTeleport;
            _includeTransitionGroup = includeTransitionGroup;
            _includeEagerTeleport = includeEagerTeleport;
            _includeInvalidNamespace = includeInvalidNamespace;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            List<VirtualNode> children = [];
            if (_includeDeferredTeleport)
            {
                children.Add(
                    new TeleportNode(
                        TeleportTargetSelector,
                        [
                            new ElementNode(
                                new QualifiedName("aside"),
                                children: [new TextNode("teleported")]),
                        ],
                        isDeferred: true));
            }

            if (_includeInvalidNamespace)
            {
                children.Add(
                    new ElementNode(
                        new QualifiedName("invalid", "urn:invalid")));
            }

            children.Add(
                new ElementNode(
                    new QualifiedName("p"),
                    children: [new TextNode("incoming component")]));
            if (_includeTransitionGroup)
            {
                children.Add(StorageTransitionGroup());
            }

            children.Add(StorageKeepAlive());
            if (_includeEagerTeleport)
            {
                children.Add(
                    new TeleportNode(
                        TeleportTargetSelector,
                        [
                            SynchronousOutgoingThenIncoming(
                                new ElementNode(
                                    new QualifiedName("aside"),
                                    children: [new TextNode("teleported eagerly")],
                                    key: "teleported")),
                        ]));
            }

            return _ => new ElementNode(
                new QualifiedName("article"),
                children: children);
        }
    }

    private sealed class StorageLeafComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static _ => new ElementNode(
                new QualifiedName("span"),
                children: [new TextNode("kept alive")]);
        }
    }

    private sealed class TrackingHydrationTriggerRegistration :
        IHydrationTriggerRegistration
    {
        public void Complete()
        {
        }

        public void Dispose()
        {
        }
    }

    private static KeepAliveNode StorageKeepAlive()
    {
        var slots = new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
        {
            ["default"] = _ => new ComponentNode(
                ComponentReference.ForType(typeof(StorageLeafComponent)),
                key: "storage-leaf"),
        };
        return new KeepAliveNode(new ComponentInvocation(slots: slots));
    }

    private static ComponentNode StorageTransitionGroup()
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tag"] = "div",
            ["css"] = false,
        };
        var slots = new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
        {
            ["default"] = _ => new FragmentNode(
                [
                    new ElementNode(
                        new QualifiedName("div"),
                        children: [new TextNode("motion item 1")],
                        key: "first"),
                    new ElementNode(
                        new QualifiedName("div"),
                        children: [new TextNode("motion item 2")],
                        key: "second"),
                ]),
        };
        return new ComponentNode(
            ComponentReference.ForType(typeof(TransitionGroup)),
            new ComponentInvocation(arguments, slots));
    }
}
