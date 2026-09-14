using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

/// <summary>Pins resolved-content adoption and client-pending recovery under [BLT-12].</summary>
public sealed class RendererSuspenseHydrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hydrate_ResolvedContent_AdoptsAndUpdatesWithLiveOrSnapshotReader(bool snapshot)
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("ready");
        HydrationWalkerHostNode serverElement = host.CreateServerElement("strong", serverText);
        host.AppendServerChild(host.Root, serverElement);
        Reference<string> message = Reactive.Reference("ready");
        int mountedCount = 0;
        int referenceCount = 0;
        ComponentRegistration content = ComponentRegistration.Define(
            "hydrated-content",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedCount++);
                return _ => new ElementNode(
                    new QualifiedName("strong"),
                    children: [new TextNode(message.Value)],
                    mountReference: value => referenceCount += value is null ? 0 : 1);
            });
        ComponentFactory components = new();
        components.Register(content);
        List<string> events = [];
        SuspenseNode root = Boundary(new ComponentNode(content.Reference), events);
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot);

        renderer.Hydrate(root, host.Root, Application(root, components, warnings));

        // [BLT-12], [HYD-4]: the runtime adds anchors/storage but adopts the existing content.
        host.Root.Children[1].ShouldBeSameAs(serverElement);
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        host.ClientCreationCount.ShouldBe(3);
        mountedCount.ShouldBe(1);
        referenceCount.ShouldBe(1);
        events.ShouldBe(["resolve"]);
        warnings.ShouldBeEmpty();
        host.Operations.ShouldNotContain(operation => operation.StartsWith("remove:", StringComparison.Ordinal));

        message.Value = "updated";
        schedulerHost.RunUntilIdle();

        host.Root.Children[1].ShouldBeSameAs(serverElement);
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        serverText.Data.ShouldBe("updated");
        mountedCount.ShouldBe(1);
        events.ShouldBe(["resolve"]);
        renderer.Render(null, host.Root);
        host.Root.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hydrate_NewDependency_ShowsFallbackAndPreservesFollowingSibling(bool snapshot)
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverContent = host.CreateServerElement("strong", host.CreateServerText("server"));
        HydrationWalkerHostNode retainedSibling = host.CreateServerElement("aside", host.CreateServerText("tail"));
        HydrationWalkerHostNode serverRoot = host.CreateServerElement("main", serverContent, retainedSibling);
        host.AppendServerChild(host.Root, serverRoot);
        TaskCompletionSource<AsynchronousComponentTarget> load = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<HydrationWrapper>(_ => load.Task);
        ComponentFactory components = Factory(definition);
        List<string> events = [];
        SuspenseNode boundary = Boundary(definition.CreateComponent(), events, timeout: 60_000);
        ElementNode root = new(
            new QualifiedName("main"),
            children: [boundary, new ElementNode(new QualifiedName("aside"), children: [new TextNode("tail")])]);
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot);

        renderer.Hydrate(root, host.Root, Application(root, components, warnings));

        // [BLT-12]: new client work uses immediate fallback even when timeout is positive.
        VisibleText(host.Root).ShouldBe("waitingtail");
        serverRoot.Children[^1].ShouldBeSameAs(retainedSibling);
        events.ShouldBe(["pending", "fallback"]);
        serverContent.Parent.ShouldBeNull();

        load.SetResult(AsynchronousComponentTarget.From<HydratedTarget>());
        schedulerHost.RunUntilIdle();

        VisibleText(host.Root).ShouldBe("resolvedtail");
        serverRoot.Children[^1].ShouldBeSameAs(retainedSibling);
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Hydrate_LazyActivationInsideHiddenContent_JoinsOwningBoundaryAndAdoptsStoredMarkup()
    {
        using RendererParityHost schedulerHost = new();
        int testThreadIdentifier = Environment.CurrentManagedThreadId;
        SynchronizationContext? testContext = SynchronizationContext.Current;
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverLazy = host.CreateServerElement("strong", host.CreateServerText("resolved"));
        HydrationWalkerHostNode serverRoot = host.CreateServerElement(
            "section",
            host.CreateServerElement("strong", host.CreateServerText("server")),
            host.CreateServerComment(HydrationMarkers.GetLazyHydrationStartData(HydrationStrategyKind.Idle)),
            serverLazy,
            host.CreateServerComment(HydrationMarkers.LazyHydrationEndData));
        host.AppendServerChild(host.Root, serverRoot);
        TaskCompletionSource<AsynchronousComponentTarget> gateLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<AsynchronousComponentTarget> lazyLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition gate = AsynchronousComponents.Define<HydrationWrapper>(_ => gateLoad.Task);
        int lazyLoadCount = 0;
        AsynchronousComponentDefinition lazy = AsynchronousComponents.Define<LazyHydrationWrapper>(_ =>
        {
            lazyLoadCount++;
            return lazyLoad.Task;
        });
        ComponentFactory components = Factory(gate);
        components.Register(lazy.Registration);
        List<string> events = [];
        SuspenseNode root = Boundary(
            new ElementNode(
                new QualifiedName("section"),
                children:
                [
                    gate.CreateComponent(),
                    lazy.CreateComponent(new ComponentInvocation(hydrationStrategy: HydrationStrategy.OnIdle())),
                ]),
            events);
        Action? activate = null;
        HydrationTriggerRegistration registration = new();
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(
            host,
            snapshot: true,
            request =>
            {
                activate = request.Trigger;
                return registration;
            });
        renderer.Hydrate(root, host.Root, Application(root, components, []));
        lazyLoadCount.ShouldBe(0);

        activate.ShouldNotBeNull()();
        schedulerHost.RunUntilIdle();
        lazyLoadCount.ShouldBe(1);
        gateLoad.SetResult(AsynchronousComponentTarget.From<HydratedTarget>());
        schedulerHost.RunUntilIdle();
        serverRoot.Children[0].Kind.ShouldBe(HydrationNodeKind.Element);

        // [BLT-12], [BLT-19], [HYD-LAZY-3]: late activation restores its stored boundary context.
        VisibleText(host.Root).ShouldBe("waiting");
        events.ShouldBe(["pending", "fallback"]);
        lazyLoad.SetResult(AsynchronousComponentTarget.From<HydratedTarget>());
        schedulerHost.RunUntilIdle();
        registration.CompletionCount.ShouldBe(1);
        // [V01.01.03.20], [BLT-13]: readiness resumes on the host before its drain returns.
        registration.CompletionThreadIdentifier.ShouldBe(testThreadIdentifier);
        registration.CompletionContext.ShouldBeSameAs(testContext);

        VisibleText(host.Root).ShouldBe("resolvedresolved");
        serverLazy.Parent.ShouldBeSameAs(serverRoot);
        host.Operations.ShouldNotContain($"remove:{serverLazy.Identifier}");
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Hydrate_PendingLazyActivationUnmounted_DropsReadinessBeforeLateLoadCompletion()
    {
        using RendererParityHost schedulerHost = new();
        int testThreadIdentifier = Environment.CurrentManagedThreadId;
        SynchronizationContext? testContext = SynchronizationContext.Current;
        HydrationWalkerFakeHost host = new();
        host.AppendServerChild(
            host.Root,
            host.CreateServerComment(HydrationMarkers.GetLazyHydrationStartData(HydrationStrategyKind.Idle)));
        host.AppendServerChild(host.Root, host.CreateServerElement("strong", host.CreateServerText("resolved")));
        host.AppendServerChild(host.Root, host.CreateServerComment(HydrationMarkers.LazyHydrationEndData));
        TaskCompletionSource<AsynchronousComponentTarget> load = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int loadCount = 0;
        int targetActivationCount = 0;
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<LazyHydrationWrapper>(_ =>
        {
            loadCount++;
            return load.Task;
        });
        ComponentFactory components = new();
        components.Register(definition.Registration);
        components.Register(new ComponentRegistration(
            ComponentReference.ForType(typeof(HydratedTarget)),
            new ComponentContract(),
            _ =>
            {
                targetActivationCount++;
                return new HydratedTarget();
            }));
        SuspenseNode root = Boundary(
            definition.CreateComponent(new ComponentInvocation(hydrationStrategy: HydrationStrategy.OnIdle())),
            []);
        Action? activate = null;
        HydrationTriggerRegistration registration = new();
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(
            host,
            snapshot: true,
            request =>
            {
                activate = request.Trigger;
                return registration;
            });
        renderer.Hydrate(root, host.Root, Application(root, components, []));

        activate.ShouldNotBeNull()();
        schedulerHost.RunUntilIdle();
        loadCount.ShouldBe(1);
        targetActivationCount.ShouldBe(0);
        registration.CompletionCount.ShouldBe(0);

        renderer.Render(null, host.Root);
        int operationCountAfterUnmount = host.Operations.Count;
        registration.DisposalCount.ShouldBe(1);
        registration.DisposalThreadIdentifier.ShouldBe(testThreadIdentifier);
        registration.DisposalContext.ShouldBeSameAs(testContext);

        // [V01.01.03.20], [BLT-13], [HYD-LAZY-3]: teardown invalidates pending readiness
        // on the same flow, so completing an abandoned load cannot revive hydration.
        load.SetResult(AsynchronousComponentTarget.From<HydratedTarget>());
        schedulerHost.RunUntilIdle();

        targetActivationCount.ShouldBe(0);
        registration.CompletionCount.ShouldBe(0);
        registration.DisposalCount.ShouldBe(1);
        host.Operations.Count.ShouldBe(operationCountAfterUnmount);
        host.Root.Children.ShouldBeEmpty();
        Scheduler.IsFlushPending.ShouldBeFalse();
    }

    [Fact]
    public void Hydrate_AlreadySettledAsynchronousComponent_AdoptsServerContentDirectly()
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverContent = host.CreateServerElement("strong", host.CreateServerText("resolved"));
        host.AppendServerChild(host.Root, serverContent);
        int loadCount = 0;
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<HydrationWrapper>(_ =>
        {
            loadCount++;
            return Task.FromResult(AsynchronousComponentTarget.From<HydratedTarget>());
        });
        List<string> events = [];
        List<string> warnings = [];
        SuspenseNode root = Boundary(definition.CreateComponent(), events);
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot: true);

        renderer.Hydrate(root, host.Root, Application(root, Factory(definition), warnings));

        // [BLT-12]: a settled target has no new client dependency and needs no fallback.
        host.Root.Children[1].ShouldBeSameAs(serverContent);
        loadCount.ShouldBe(1);
        events.ShouldBe(["resolve"]);
        warnings.ShouldBeEmpty();
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Hydrate_EmptyDefaultSlot_DoesNotClaimFollowingServerSibling()
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverSibling = host.CreateServerElement("aside");
        HydrationWalkerHostNode serverRoot = host.CreateServerElement("main", serverSibling);
        host.AppendServerChild(host.Root, serverRoot);
        List<string> events = [];
        ElementNode root = new(
            new QualifiedName("main"),
            children: [Boundary(null, events), new ElementNode(new QualifiedName("aside"))]);
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot: true);

        renderer.Hydrate(root, host.Root);

        // [BLT-12]: an empty slot has no server range, so runtime anchors precede the sibling.
        serverRoot.Children[^1].ShouldBeSameAs(serverSibling);
        host.Operations.ShouldNotContain(operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        events.ShouldBe(["resolve"]);
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Hydrate_NestedResolvedBoundaries_AdoptsContentAndResolvesInnerFirst()
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("ready");
        host.AppendServerChild(host.Root, serverText);
        List<string> events = [];
        SuspenseNode inner = Boundary(new TextNode("ready"), events, prefix: "inner:");
        SuspenseNode root = Boundary(inner, events, prefix: "outer:");
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot: true);

        renderer.Hydrate(root, host.Root);

        // [BLT-12], [BLT-20]: nested resolved content is already visible, inner notification first.
        host.Root.Children[2].ShouldBeSameAs(serverText);
        events.ShouldBe(["inner:resolve", "outer:resolve"]);
        VisibleText(host.Root).ShouldBe("ready");
        renderer.Render(null, host.Root);
        host.Root.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hydrate_PendingAdoptedSibling_ReleasesEffectsOnlyWhenContentReveals(bool abandon)
    {
        using RendererParityHost schedulerHost = new();
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverRoot = host.CreateServerElement(
            "section",
            host.CreateServerElement("span", host.CreateServerText("ready")),
            host.CreateServerElement("strong", host.CreateServerText("server")));
        host.AppendServerChild(host.Root, serverRoot);
        TaskCompletionSource<AsynchronousComponentTarget> load = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<HydrationWrapper>(_ => load.Task);
        ComponentFactory components = Factory(definition);
        int mountedCount = 0;
        int postCount = 0;
        List<object?> references = [];
        Reference<int> state = Reactive.Reference(0);
        ComponentRegistration ready = ComponentRegistration.Define(
            "ready-child",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedCount++);
                Reactive.Watch(
                    () => state.Value,
                    (_, _, _) => postCount++,
                    new WatchOptions { Flush = WatchFlushMode.Post, Scheduler = context.WatchScheduler });
                return _ => new ElementNode(
                    new QualifiedName("span"),
                    children: [new TextNode("ready")],
                    mountReference: value => references.Add(value));
            });
        components.Register(ready);
        List<string> events = [];
        SuspenseNode root = Boundary(
            new ElementNode(
                new QualifiedName("section"),
                children: [new ComponentNode(ready.Reference), definition.CreateComponent()]),
            events);
        Renderer<HydrationWalkerHostNode> renderer = CreateRenderer(host, snapshot: true);
        renderer.Hydrate(root, host.Root, Application(root, components, []));
        state.Value = 1;
        schedulerHost.RunUntilIdle();

        // [BLT-12], [BLT-18]: adopted siblings share hidden-branch effect ownership.
        mountedCount.ShouldBe(0);
        postCount.ShouldBe(0);
        references.ShouldBeEmpty();
        VisibleText(host.Root).ShouldBe("waiting");
        if (abandon)
        {
            renderer.Render(null, host.Root);
        }

        load.SetResult(AsynchronousComponentTarget.From<HydratedTarget>());
        schedulerHost.RunUntilIdle();
        if (abandon)
        {
            mountedCount.ShouldBe(0);
            postCount.ShouldBe(0);
            references.ShouldBeEmpty();
            events.ShouldBe(["pending", "fallback"]);
        }
        else
        {
            events.ShouldBe(["pending", "fallback", "resolve"]);
            mountedCount.ShouldBe(1);
            postCount.ShouldBe(1);
            references.ShouldHaveSingleItem();
            VisibleText(host.Root).ShouldBe("readyresolved");
            renderer.Render(null, host.Root);
        }

        host.Root.Children.ShouldBeEmpty();
    }

    private static SuspenseNode Boundary(
        VirtualNode? content,
        List<string> events,
        int? timeout = null,
        string prefix = "") =>
        new(new ComponentInvocation(
            arguments: timeout is { } duration ? new Dictionary<string, object?> { ["timeout"] = duration } : null,
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => content,
                ["fallback"] = _ => new TextNode("waiting"),
            },
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["pending"] = _ => events.Add(prefix + "pending"),
                ["fallback"] = _ => events.Add(prefix + "fallback"),
                ["resolve"] = _ => events.Add(prefix + "resolve"),
            }));

    private static ComponentFactory Factory(AsynchronousComponentDefinition definition)
    {
        ComponentFactory components = new();
        components.Register(definition.Registration);
        components.Register(new ComponentRegistration(
            ComponentReference.ForType(typeof(HydratedTarget)),
            new ComponentContract(),
            _ => new HydratedTarget()));
        return components;
    }

    private static ApplicationContext Application(VirtualNode root, ComponentFactory components, List<string> warnings) =>
        new(new ApplicationOptions { RootComponent = root, Components = components, WarnHandler = warnings.Add });

    private static string VisibleText(HydrationWalkerHostNode node) =>
        node.Kind == HydrationNodeKind.Text ? node.Data : string.Concat(node.Children.Select(VisibleText));

    private static Renderer<HydrationWalkerHostNode> CreateRenderer(
        HydrationWalkerFakeHost host,
        bool snapshot,
        Func<HydrationTriggerRequest<HydrationWalkerHostNode>, IHydrationTriggerRegistration>? scheduleHydrationTrigger = null)
    {
        RendererOptions<HydrationWalkerHostNode> options = host.Options;
        return RendererFactory.CreateRenderer(new RendererOptions<HydrationWalkerHostNode>
        {
            Insert = options.Insert,
            Remove = options.Remove,
            CreateElement = options.CreateElement,
            CreateText = options.CreateText,
            CreateComment = options.CreateComment,
            SetText = options.SetText,
            ParentNode = options.ParentNode,
            NextSibling = options.NextSibling,
            PatchAttribute = options.PatchAttribute,
            CreateHydrationReader = snapshot ? root => new SnapshotReader(root) : options.CreateHydrationReader,
            ScheduleHydrationTrigger = scheduleHydrationTrigger,
        });
    }

    private sealed class SnapshotReader : HydrationNodeReader<HydrationWalkerHostNode>
    {
        private readonly Dictionary<HydrationWalkerHostNode, Snapshot> _nodes = [];

        internal SnapshotReader(HydrationWalkerHostNode root) => Capture(root);

        public override HydrationNodeKind Kind(HydrationWalkerHostNode node) => _nodes[node].Kind;
        public override HydrationWalkerHostNode? FirstChild(HydrationWalkerHostNode node) => _nodes[node].First;
        public override HydrationWalkerHostNode? NextSibling(HydrationWalkerHostNode node) => _nodes[node].Next;
        public override HydrationWalkerHostNode? ParentNode(HydrationWalkerHostNode node) => _nodes[node].Parent;
        public override string ElementTag(HydrationWalkerHostNode node) => _nodes[node].Data;
        public override string Data(HydrationWalkerHostNode node) => _nodes[node].Data;
        public override string? Attribute(HydrationWalkerHostNode node, string name) => _nodes[node].Attributes.GetValueOrDefault(name);

        private void Capture(HydrationWalkerHostNode node)
        {
            int index = node.Parent?.Children.IndexOf(node) ?? -1;
            HydrationWalkerHostNode? next = index >= 0 && index + 1 < node.Parent!.Children.Count
                ? node.Parent.Children[index + 1] : null;
            _nodes.Add(node, new Snapshot(node.Kind, node.Data, node.Parent, node.Children.FirstOrDefault(), next, new(node.Attributes)));
            foreach (HydrationWalkerHostNode child in node.Children)
            {
                Capture(child);
            }
        }

        private sealed record Snapshot(
            HydrationNodeKind Kind,
            string Data,
            HydrationWalkerHostNode? Parent,
            HydrationWalkerHostNode? First,
            HydrationWalkerHostNode? Next,
            Dictionary<string, string> Attributes);
    }

    private sealed class HydrationWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => throw new NotSupportedException();
    }

    private sealed class LazyHydrationWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => throw new NotSupportedException();
    }

    private sealed class HydrationTriggerRegistration : IHydrationTriggerRegistration
    {
        internal int CompletionCount { get; private set; }

        internal int CompletionThreadIdentifier { get; private set; }

        internal SynchronizationContext? CompletionContext { get; private set; }

        internal int DisposalCount { get; private set; }

        internal int DisposalThreadIdentifier { get; private set; }

        internal SynchronizationContext? DisposalContext { get; private set; }

        public void Complete()
        {
            CompletionCount++;
            CompletionThreadIdentifier = Environment.CurrentManagedThreadId;
            CompletionContext = SynchronizationContext.Current;
        }

        public void Dispose()
        {
            DisposalCount++;
            DisposalThreadIdentifier = Environment.CurrentManagedThreadId;
            DisposalContext = SynchronizationContext.Current;
        }
    }

    private sealed class HydratedTarget : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new ElementNode(new QualifiedName("strong"), children: [new TextNode("resolved")]);
    }
}
