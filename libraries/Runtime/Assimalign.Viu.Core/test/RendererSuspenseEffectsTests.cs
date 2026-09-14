using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererSuspenseEffectsTests
{
    [Fact]
    public void Suspense_HiddenBranch_DefersMountedReferencesAndScheduledPostWatchUntilReveal()
    {
        // [BLT-13]: hidden host state is not published through mounted callbacks or references.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        Reference<int> state = Reactive.Reference(0);
        int mountedRuns = 0;
        int updatedRuns = 0;
        int postRuns = 0;
        List<object?> references = [];
        MountReference reference = value => references.Add(value);
        ComponentRegistration probe = ComponentRegistration.Define(
            "effect-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() =>
                {
                    references.Count.ShouldBe(1);
                    mountedRuns++;
                });
                context.Lifecycle.OnUpdated(() => updatedRuns++);
                Reactive.Watch(
                    () => state.Value,
                    (_, _, _) =>
                    {
                        references.Count.ShouldBe(1);
                        postRuns++;
                    },
                    new WatchOptions { Flush = WatchFlushMode.Post, Scheduler = context.WatchScheduler });
                return _ => new ElementNode(
                    new QualifiedName("span"),
                    children: [new TextNode(state.Value.ToString())],
                    mountReference: reference);
            });
        ComponentFactory components = Factory(asynchronous, probe);
        SuspenseNode root = Boundary(new FragmentNode(
        [
            new ComponentNode(probe.Reference),
            asynchronous.CreateComponent(),
        ]));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, components));
        state.Value = 1;
        state.Value = 2;
        host.RunUntilIdle();

        mountedRuns.ShouldBe(0);
        updatedRuns.ShouldBe(0);
        postRuns.ShouldBe(0);
        references.ShouldBeEmpty();

        load.SetResult(AsynchronousComponentTarget.From<Target>());
        host.RunUntilIdle();

        mountedRuns.ShouldBe(1);
        updatedRuns.ShouldBe(1);
        postRuns.ShouldBe(1);
        references.ShouldHaveSingleItem().ShouldBeOfType<RendererParityNode>()
            .DescendantText.ShouldBe("2");
        state.Value = 3;
        host.RunUntilIdle();
        mountedRuns.ShouldBe(1);
        updatedRuns.ShouldBe(2);
        postRuns.ShouldBe(2);
        references.Count.ShouldBe(1);
        renderer.Render(null, host.Container);
        references.Count.ShouldBe(2);
        references[1].ShouldBeNull();
    }

    [Fact]
    public void Suspense_NeverRevealedUnmount_DiscardsMountedReferencesAndPostWatch()
    {
        // [BLT-13]: discarded hidden work never publishes host state.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        Reference<int> state = Reactive.Reference(0);
        int mountedRuns = 0;
        int postRuns = 0;
        int referenceRuns = 0;
        ComponentRegistration probe = ComponentRegistration.Define(
            "discarded-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedRuns++);
                Reactive.Watch(
                    () => state.Value,
                    (_, _, _) => postRuns++,
                    new WatchOptions { Flush = WatchFlushMode.Post, Scheduler = context.WatchScheduler });
                return _ => new ElementNode(new QualifiedName("span"),
                    mountReference: _ => referenceRuns++);
            });
        SuspenseNode root = Boundary(new FragmentNode(
        [
            new ComponentNode(probe.Reference),
            asynchronous.CreateComponent(),
        ]));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, Factory(asynchronous, probe)));
        state.Value = 1;
        renderer.Render(null, host.Container);
        host.RunUntilIdle();

        mountedRuns.ShouldBe(0);
        postRuns.ShouldBe(0);
        referenceRuns.ShouldBe(0);
    }

    [Fact]
    public void Suspense_HiddenRootReferenceReplaced_AssignsOnlyLatestReference()
    {
        // [BLT-13]: references belong to the revealed occurrence, including structural slot roots.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        int oldReferenceRuns = 0;
        int nextReferenceRuns = 0;
        SuspenseNode MakeBoundary(MountReference reference) => Boundary(new ElementNode(
            new QualifiedName("span"),
            children: [asynchronous.CreateComponent()],
            mountReference: reference));
        SuspenseNode initial = MakeBoundary(_ => oldReferenceRuns++);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, Application(initial, Factory(asynchronous)));
        renderer.Render(MakeBoundary(_ => nextReferenceRuns++), host.Container);
        oldReferenceRuns.ShouldBe(0);
        nextReferenceRuns.ShouldBe(0);
        load.SetResult(AsynchronousComponentTarget.From<Target>());
        host.RunUntilIdle();

        oldReferenceRuns.ShouldBe(0);
        nextReferenceRuns.ShouldBe(1);
        renderer.Render(null, host.Container);
        oldReferenceRuns.ShouldBe(0);
        nextReferenceRuns.ShouldBe(2);
    }

    [Fact]
    public void Suspense_LastPendingWrapperRemoved_RevealsRemainingContentWithoutWaitingForLoader()
    {
        // [BLT-13]: a discarded dependency occurrence no longer blocks its generation.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        Reference<bool> includeDependency = Reactive.Reference(true);
        int mountedRuns = 0;
        ComponentRegistration probe = ComponentRegistration.Define(
            "removal-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedRuns++);
                return _ => includeDependency.Value
                    ? asynchronous.CreateComponent()
                    : new ElementNode(new QualifiedName("span"), children: [new TextNode("removed")]);
            });
        SuspenseNode root = Boundary(new ComponentNode(probe.Reference));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, Factory(asynchronous, probe)));
        mountedRuns.ShouldBe(0);
        includeDependency.Value = false;
        host.RunUntilIdle();

        mountedRuns.ShouldBe(1);
        load.Task.IsCompleted.ShouldBeFalse();
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_SharedLoaderOneWrapperRemoved_WaitsForRemainingOccurrence()
    {
        // [BLT-13]: each mounted occurrence owns one lease even when its loader task is shared.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        Reference<bool> includeFirst = Reactive.Reference(true);
        int mountedRuns = 0;
        ComponentRegistration probe = ComponentRegistration.Define(
            "shared-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedRuns++);
                return _ => new FragmentNode(includeFirst.Value
                    ? [asynchronous.CreateComponent(key: "first"), asynchronous.CreateComponent(key: "second")]
                    : [asynchronous.CreateComponent(key: "second")]);
            });
        SuspenseNode root = Boundary(new ComponentNode(probe.Reference));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, Factory(asynchronous, probe)));
        includeFirst.Value = false;
        host.RunUntilIdle();
        mountedRuns.ShouldBe(0);
        load.SetResult(AsynchronousComponentTarget.From<Target>());
        host.RunUntilIdle();

        mountedRuns.ShouldBe(1);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_LateAsynchronousDescendant_MountedCallbacksRemainChildFirst()
    {
        // [BLT-13], [SCH-4]: deferral retains child-before-parent mounted ordering.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        List<string> callbacks = [];
        ComponentRegistration child = ComponentRegistration.Define(
            "late-child",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => callbacks.Add("child"));
                return _ => new TextNode("ready");
            });
        ComponentRegistration parent = ComponentRegistration.Define(
            "early-parent",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => callbacks.Add("parent"));
                return _ => asynchronous.CreateComponent();
            });
        ComponentFactory components = Factory(asynchronous, parent);
        components.Register(child);
        SuspenseNode root = Boundary(new ComponentNode(parent.Reference));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, components));
        callbacks.ShouldBeEmpty();
        load.SetResult(new AsynchronousComponentTarget(child.Reference));
        host.RunUntilIdle();

        callbacks.ShouldBe(["child", "parent"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_PreviousContentRetainedDuringReplacement_ContinuesVisiblePostEffects()
    {
        // [BLT-13]: replacing the candidate does not suspend the previous visible generation.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        Reference<int> state = Reactive.Reference(0);
        int postRuns = 0;
        int updatedRuns = 0;
        ComponentRegistration previous = ComponentRegistration.Define(
            "previous-visible",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnUpdated(() => updatedRuns++);
                Reactive.Watch(
                    () => state.Value,
                    (_, _, _) => postRuns++,
                    new WatchOptions { Flush = WatchFlushMode.Post, Scheduler = context.WatchScheduler });
                return _ => new TextNode(state.Value.ToString());
            });
        SuspenseNode initial = Boundary(new ComponentNode(previous.Reference));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, Application(initial, Factory(asynchronous, previous)));
        renderer.Render(Boundary(asynchronous.CreateComponent()), host.Container);
        state.Value = 1;
        host.RunUntilIdle();

        updatedRuns.ShouldBe(1);
        postRuns.ShouldBe(1);
        load.Task.IsCompleted.ShouldBeFalse();
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_ResolvedMatchingRootAddsAsynchronousChild_LoadsIndependentlyWithoutPendingAgain()
    {
        // [BLT-19]: resolved root identity preserves its live tree; only replacement roots open a generation.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(
            new AsynchronousComponentOptions
            {
                Loader = _ => load.Task,
                Delay = 0,
                LoadingComponent = _ => new TextNode("independent-loading"),
            });
        List<string> events = [];
        SuspenseNode CreateBoundary(VirtualNode child) => new(new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["timeout"] = 0 },
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => new ElementNode(new QualifiedName("span"), children: [child]),
                ["fallback"] = _ => new TextNode("boundary-fallback"),
            },
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["pending"] = _ => events.Add("pending"),
                ["fallback"] = _ => events.Add("fallback"),
                ["resolve"] = _ => events.Add("resolve"),
            }));
        SuspenseNode initial = CreateBoundary(new TextNode("before"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, Application(initial, Factory(asynchronous)));
        renderer.Render(CreateBoundary(asynchronous.CreateComponent()), host.Container);
        host.RunUntilIdle();

        events.ShouldBe(["resolve"]);
        host.Container.Children.Find(node => node.Kind == RendererParityNodeKind.Element)!
            .DescendantText.ShouldBe("independent-loading");
        load.SetResult(AsynchronousComponentTarget.From<Target>());
        host.RunUntilIdle();

        events.ShouldBe(["resolve"]);
        host.Container.Children.Find(node => node.Kind == RendererParityNodeKind.Element)!
            .DescendantText.ShouldBe("ready");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_FallbackResolveAndDeferredMounted_ObserveCommittedHostChanges()
    {
        // [BLT-17], [BLT-18], [SCH-10]: visibility events and released lifecycle observe a host commit.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(_ => load.Task);
        int beforeFallback = host.CommitCount;
        int beforeReveal = 0;
        int fallbackRuns = 0;
        int resolveRuns = 0;
        int mountedRuns = 0;
        ComponentRegistration probe = ComponentRegistration.Define(
            "committed-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() =>
                {
                    host.CommitCount.ShouldBeGreaterThan(beforeReveal);
                    mountedRuns++;
                });
                return _ => new TextNode("probe");
            });
        SuspenseNode root = new(new ComponentInvocation(
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => new FragmentNode(
                    [new ComponentNode(probe.Reference), asynchronous.CreateComponent()]),
                ["fallback"] = _ => new TextNode("waiting"),
            },
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["fallback"] = _ =>
                {
                    host.CommitCount.ShouldBeGreaterThan(beforeFallback);
                    fallbackRuns++;
                },
                ["resolve"] = _ =>
                {
                    host.CommitCount.ShouldBeGreaterThan(beforeReveal);
                    resolveRuns++;
                },
            }));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, Factory(asynchronous, probe)));
        host.RunUntilIdle();
        fallbackRuns.ShouldBe(1);
        resolveRuns.ShouldBe(0);
        mountedRuns.ShouldBe(0);
        beforeReveal = host.CommitCount;
        load.SetResult(AsynchronousComponentTarget.From<Target>());
        host.RunUntilIdle();

        fallbackRuns.ShouldBe(1);
        resolveRuns.ShouldBe(1);
        mountedRuns.ShouldBe(1);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_AlreadyFaultedLoader_EstablishesFallbackBeforeRoutingFailureOnce()
    {
        // [BLT-17], [BLT-21]: synchronous loader faults follow the same pending/fallback failure path.
        using var host = new RendererParityHost();
        InvalidOperationException failure = new("already failed");
        AsynchronousComponentDefinition asynchronous = AsynchronousComponents.Define<Wrapper>(
            _ => Task.FromException<AsynchronousComponentTarget>(failure));
        List<string> events = [];
        List<Exception> errors = [];
        int mountedRuns = 0;
        ComponentRegistration probe = ComponentRegistration.Define(
            "failed-probe",
            new ComponentContract(),
            context =>
            {
                context.Lifecycle.OnMounted(() => mountedRuns++);
                return _ => new TextNode("hidden");
            });
        SuspenseNode root = new(new ComponentInvocation(
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => new FragmentNode(
                    [new ComponentNode(probe.Reference), asynchronous.CreateComponent()]),
                ["fallback"] = _ => new ElementNode(new QualifiedName("span"), children: [new TextNode("waiting")]),
            },
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["pending"] = _ => events.Add("pending"),
                ["fallback"] = _ => events.Add("fallback"),
                ["resolve"] = _ => events.Add("resolve"),
            }));
        ApplicationContext application = new(new ApplicationOptions
        {
            RootComponent = root,
            Components = Factory(asynchronous, probe),
            ErrorHandler = (error, _, source) =>
            {
                source.ShouldBe("suspense dependency");
                events.Add("error");
                errors.Add(error);
            },
        });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        host.RunUntilIdle();

        events.ShouldBe(["pending", "fallback", "error"]);
        errors.ShouldHaveSingleItem().ShouldBeSameAs(failure);
        mountedRuns.ShouldBe(0);
        host.Container.Children.Find(node => node.Kind == RendererParityNodeKind.Element)!
            .DescendantText.ShouldBe("waiting");
        renderer.Render(null, host.Container);
    }

    private static SuspenseNode Boundary(VirtualNode content) => new(
        new ComponentInvocation(slots: new Dictionary<string, ComponentSlot>
        {
            ["default"] = _ => content,
            ["fallback"] = _ => new TextNode("waiting"),
        }));

    private static ComponentFactory Factory(
        AsynchronousComponentDefinition asynchronous,
        ComponentRegistration? probe = null)
    {
        ComponentFactory components = new();
        components.Register(asynchronous.Registration);
        components.Register(new ComponentRegistration(
            ComponentReference.ForType(typeof(Target)),
            new ComponentContract(),
            _ => new Target()));
        if (probe is not null)
        {
            components.Register(probe);
        }

        return components;
    }

    private static ApplicationContext Application(VirtualNode root, ComponentFactory components) =>
        new(new ApplicationOptions { RootComponent = root, Components = components });

    private sealed class Wrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class Target : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => new TextNode("ready");
    }
}
