using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins the Suspense state machine specified by [BLT-13] and [BLT-16] through [BLT-21].
/// </summary>
public sealed class RendererSuspenseTests
{
    [Fact]
    public void Suspense_MultipleDependencies_RevealsOnlyAfterEveryLoadSettles()
    {
        // [BLT-13] Every dependency must settle before the hidden branch is revealed.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> firstLoad = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<AsynchronousComponentTarget> secondLoad = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition first = Define<FirstWrapper>(firstLoad);
        AsynchronousComponentDefinition second = Define<SecondWrapper>(secondLoad);
        SuspenseNode root = Suspense(
            new FragmentNode(
            [
                Request(first, "first"),
                Request(second, "second"),
            ]),
            new TextNode("waiting"));
        ComponentFactory components = CreateFactory(first, second);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, components));

        VisibleText(host.Container).ShouldBe("waiting");

        host.RunUntilIdle();
        firstLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("waiting");

        host.RunUntilIdle();
        secondLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("firstsecond");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_NestedBoundary_ResolvesInnerBeforeOuter()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        // [BLT-20] The inner reveal holds an outer dependency; events are inner-first.
        List<string> events = [];
        SuspenseNode inner = Suspense(
            Request(definition, "inner-resolved"),
            new TextNode("inner-fallback"),
            listeners: Listeners(events, "inner:"));
        SuspenseNode root = Suspense(inner, new TextNode("outer-fallback"),
            listeners: Listeners(events, "outer:"));
        ComponentFactory components = CreateFactory(definition);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, components));

        VisibleText(host.Container).ShouldBe("outer-fallback");

        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("inner-resolved");
        events.FindAll(value => value.EndsWith("resolve", StringComparison.Ordinal))
            .ShouldBe(["inner:resolve", "outer:resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_UpdateWhilePending_RefreshesFallbackAndHiddenContentBeforeReveal()
    {
        // [BLT-19] Matching pending content is patched in its storage container.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        SuspenseNode initial = Suspense(
            Request(definition, "first"),
            new TextNode("loading-first"));
        ComponentFactory components = CreateFactory(definition);
        ApplicationContext application = CreateApplication(initial, components);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, application);

        VisibleText(host.Container).ShouldBe("loading-first");

        SuspenseNode updated = Suspense(
            Request(definition, "second"),
            new TextNode("loading-second"));
        renderer.Render(updated, host.Container);

        VisibleText(host.Container).ShouldBe("loading-second");

        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("second");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_RejectedDependency_RoutesOnceAndKeepsFallbackVisible()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        SuspenseNode root = Suspense(
            definition.CreateComponent(),
            new TextNode("waiting"));
        ComponentFactory components = CreateFactory(definition);
        // [BLT-21] Failure keeps the visible branch and does not emit resolve.
        List<Exception> handled = [];
        List<string> sources = [];
        ApplicationContext application = CreateApplication(
            root,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, source) =>
                {
                    handled.Add(error);
                    sources.Add(source);
                },
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        VisibleText(host.Container).ShouldBe("waiting");

        host.RunUntilIdle();
        load.SetException(new InvalidOperationException("load failed"));
        host.RunUntilIdle();

        handled.ShouldHaveSingleItem()
            .ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("load failed");
        sources.ShouldBe(["suspense dependency"]);
        VisibleText(host.Container).ShouldBe("waiting");
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Suspense_FirstPendingMountWithoutPositiveTimeout_ShowsFallback(int? timeout)
    {
        // [BLT-16] An initial mount has no previous content to retain.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode root = Suspense(Request(definition, "ready"), new TextNode("waiting"),
            timeout, Listeners(events));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(definition)));

        VisibleText(host.Container).ShouldBe("waiting");
        events.ShouldBe(["pending", "fallback"]);
        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("ready");
        events.ShouldBe(["pending", "fallback", "resolve"]);
        host.RunUntilIdle();
        events.Count.ShouldBe(3);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_FirstPendingMountWithPositiveTimeout_ShowsFallbackOnlyAtDeadline()
    {
        // [BLT-16] An explicit positive timeout also applies without a previous content branch.
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode root = Suspense(Request(definition, "ready"), new TextNode("waiting"),
            100, Listeners(events));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(definition)));
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe(string.Empty);
        events.ShouldBe(["pending"]);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe(string.Empty);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("waiting");
        events.ShouldBe(["pending", "fallback"]);

        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("ready");
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void Suspense_AbsentOrNegativeTimeout_RetainsPreviousContent(int? timeout)
    {
        // [BLT-16], [BLT-17] A pending replacement preserves the visible branch indefinitely.
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"),
            listeners: Listeners(events));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, CreateApplication(initial, CreateFactory(definition)));
        events.ShouldBe(["resolve"]);

        renderer.Render(Suspense(Request(definition, "next"), new TextNode("waiting"),
            timeout, Listeners(events)), host.Container);
        clock.Advance(TimeSpan.FromDays(1));
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("previous");
        events.ShouldBe(["resolve", "pending"]);
        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("next");
        events.ShouldBe(["resolve", "pending", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_ZeroTimeout_ShowsFallbackAtPendingThenRevealsContent()
    {
        // [BLT-16], [BLT-17] A zero timeout emits pending then fallback in the same render.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, CreateApplication(initial, CreateFactory(definition)));
        Dictionary<string, ComponentEventListener> listeners = new(StringComparer.Ordinal)
        {
            ["pending"] = _ => events.Add("pending:" + VisibleText(host.Container)),
            ["fallback"] = _ => events.Add("fallback:" + VisibleText(host.Container)),
            ["resolve"] = _ => events.Add("resolve:" + VisibleText(host.Container)),
        };

        renderer.Render(Suspense(Request(definition, "next"), new TextNode("waiting"),
            0, listeners), host.Container);

        VisibleText(host.Container).ShouldBe("waiting");
        events.ShouldBe(["pending:previous", "fallback:waiting"]);
        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("next");
        events.ShouldBe(["pending:previous", "fallback:waiting", "resolve:next"]);
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData("100")]
    [InlineData(100L)]
    [InlineData(true)]
    public void Suspense_InvalidTimeoutType_RejectsBeforeMutatingHost(object timeout)
    {
        // [BLT-16] A timeout is an integer argument, with no implicit runtime conversions.
        using var host = new RendererParityHost();
        var root = new SuspenseNode(new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["timeout"] = timeout }));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        Should.Throw<ArgumentException>(() => renderer.Render(root, host.Container))
            .Message.ShouldContain("integer");
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Suspense_PositiveTimeout_UsesSchedulerClockAndShowsFallbackOnlyAtDeadline()
    {
        // [BLT-16] Advancing the scheduler clock controls the deadline without wall-clock sleeps.
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, CreateApplication(initial, CreateFactory(definition)));
        renderer.Render(Suspense(Request(definition, "next"), new TextNode("waiting"),
            100, Listeners(events)), host.Container);
        host.RunUntilIdle();

        clock.Advance(TimeSpan.FromMilliseconds(99));
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("previous");
        events.ShouldBe(["pending"]);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("waiting");
        events.ShouldBe(["pending", "fallback"]);

        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("next");
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_ResolveBeforeDeadline_CancelsFallbackTimer()
    {
        // [BLT-16] Completion invalidates a pending timer; it cannot later replace resolved content.
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, CreateApplication(initial, CreateFactory(definition)));
        renderer.Render(Suspense(Request(definition, "next"), new TextNode("waiting"),
            100, Listeners(events)), host.Container);
        host.RunUntilIdle();
        clock.Advance(TimeSpan.FromMilliseconds(99));

        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("next");
        events.ShouldBe(["pending", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_DependencyArrivesWhilePending_JoinsCurrentSetWithoutAnotherPendingEvent()
    {
        // [BLT-19] A patched pending branch adds dependencies to its current generation.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> firstLoad = NewLoad();
        TaskCompletionSource<AsynchronousComponentTarget> secondLoad = NewLoad();
        AsynchronousComponentDefinition first = Define<FirstWrapper>(firstLoad);
        AsynchronousComponentDefinition second = Define<SecondWrapper>(secondLoad);
        List<string> events = [];
        SuspenseNode root = Suspense(new FragmentNode([Request(first, "first")]),
            new TextNode("waiting"), listeners: Listeners(events));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(first, second)));

        renderer.Render(Suspense(new FragmentNode([Request(first, "first"), Request(second, "second")]),
            new TextNode("waiting"), listeners: Listeners(events)), host.Container);
        events.ShouldBe(["pending", "fallback"]);
        host.RunUntilIdle();
        firstLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("waiting");
        events.Count.ShouldBe(2);
        host.RunUntilIdle();
        secondLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("firstsecond");
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_ReactiveUpdateWhilePending_PatchesStoredContent()
    {
        // [BLT-19] Component render jobs continue in storage while visible fallback is unchanged.
        using var host = new RendererParityHost();
        Reference<string> message = Reactive.Reference("before");
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        ComponentFactory components = CreateFactory(definition);
        int renders = 0;
        ComponentRegistration registration = ComponentRegistration.Define(
            "reactive-pending", new ComponentContract(), _ => _ =>
            {
                renders++;
                return new FragmentNode([new TextNode(message.Value), Request(definition, "target")]);
            });
        components.Register(registration);
        SuspenseNode root = Suspense(new ComponentNode(registration.Reference), new TextNode("waiting"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, CreateApplication(root, components));

        message.Value = "after";
        host.RunUntilIdle();
        renders.ShouldBe(2);
        VisibleText(host.Container).ShouldBe("waiting");
        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("aftertarget");
        renders.ShouldBe(2);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_ReplacedPendingBranch_DiscardsOldDependencyAndKeepsPreviousContent()
    {
        // [BLT-19] A replacement abandons the old generation without altering the active branch.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> firstLoad = NewLoad();
        TaskCompletionSource<AsynchronousComponentTarget> secondLoad = NewLoad();
        AsynchronousComponentDefinition first = AsynchronousComponents.Define<FirstWrapper>(
            new AsynchronousComponentOptions { Loader = _ => firstLoad.Task, Delay = 0 });
        AsynchronousComponentDefinition second = Define<SecondWrapper>(secondLoad);
        List<string> events = [];
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"));
        FragmentNode WithObserver(SuspenseNode boundary) =>
            new([boundary, Request(first, "observer")]);
        FragmentNode initialRoot = WithObserver(initial);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initialRoot, host.Container, CreateApplication(initialRoot, CreateFactory(first, second)));
        renderer.Render(WithObserver(Suspense(Request(first, "discarded"), new TextNode("waiting"),
            listeners: Listeners(events))), host.Container);
        renderer.Render(WithObserver(Suspense(Request(second, "kept"), new TextNode("waiting"),
            listeners: Listeners(events))), host.Container);
        VisibleText(host.Container).ShouldBe("previous");
        events.ShouldBe(["pending", "pending"]);

        host.RunUntilIdle();
        secondLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("kept");
        host.RunUntilIdle();
        firstLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        // The independent observer proves the abandoned shared load actually finished.
        VisibleText(host.Container).ShouldBe("keptobserver");
        events.ShouldBe(["pending", "pending", "resolve"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_NonSuspensibleComponent_RendersItsOwnLoadingBranch()
    {
        // [BLT-20] Opting out never starts a boundary dependency set.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<FirstWrapper>(
            new AsynchronousComponentOptions
            {
                Loader = _ => load.Task,
                Suspensible = false,
                Delay = 0,
                LoadingComponent = _ => new TextNode("self-loading"),
            });
        List<string> events = [];
        SuspenseNode root = Suspense(Request(definition, "ready"), new TextNode("boundary-fallback"),
            listeners: Listeners(events));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, CreateFactory(definition)));
        VisibleText(host.Container).ShouldBe("self-loading");
        events.ShouldBe(["resolve"]);
        host.RunUntilIdle();
        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        host.RunUntilIdle();
        VisibleText(host.Container).ShouldBe("ready");
        events.ShouldBe(["resolve"]);
        renderer.Render(null, host.Container);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Suspense_DependencyError_TraversesCaptureChainAndHonorsStop(bool stopAtParent)
    {
        // [BLT-21], [CMP-23] Capture starts at the nearest ancestor; false stops propagation.
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        ComponentFactory components = CreateFactory(definition);
        List<string> captured = [];
        List<string> events = [];
        ComponentRegistration parent = ComponentRegistration.Define(
            "capturing-parent", new ComponentContract(), context =>
            {
                context.Lifecycle.OnErrorCaptured((error, _, source) =>
                {
                    captured.Add($"parent:{source}:{error.Message}");
                    return !stopAtParent;
                });
                return _ => Suspense(Request(definition, "ready"), new TextNode("waiting"),
                    listeners: Listeners(events));
            });
        components.Register(parent);
        ComponentRegistration ancestor = ComponentRegistration.Define(
            "capturing-ancestor", new ComponentContract(), context =>
            {
                context.Lifecycle.OnErrorCaptured((error, _, source) =>
                {
                    captured.Add($"ancestor:{source}:{error.Message}");
                    return true;
                });
                return _ => new ComponentNode(parent.Reference);
            });
        components.Register(ancestor);
        var root = new ComponentNode(ancestor.Reference);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ApplicationContext application = CreateApplication(root, components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, source) => captured.Add($"application:{source}:{error.Message}"),
            });
        renderer.Render(root, host.Container, application);

        host.RunUntilIdle();
        load.SetException(new InvalidOperationException("failure"));
        host.RunUntilIdle();

        captured.ShouldBe(stopAtParent
            ? ["parent:suspense dependency:failure"]
            : ["parent:suspense dependency:failure", "ancestor:suspense dependency:failure",
                "application:suspense dependency:failure"]);
        VisibleText(host.Container).ShouldBe("waiting");
        events.ShouldBe(["pending", "fallback"]);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Suspense_DependencyErrorBeforeTimeout_KeepsPreviousContentAndCancelsFallback()
    {
        // [BLT-21] A failed generation freezes visible content, including after its old deadline.
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = NewLoad();
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        List<string> events = [];
        int errors = 0;
        SuspenseNode initial = Suspense(new TextNode("previous"), new TextNode("waiting"));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, CreateApplication(initial, CreateFactory(definition),
            new ApplicationOptions { ErrorHandler = (_, _, _) => errors++ }));
        renderer.Render(Suspense(Request(definition, "next"), new TextNode("waiting"),
            100, Listeners(events)), host.Container);

        host.RunUntilIdle();
        load.SetException(new InvalidOperationException("failure"));
        host.RunUntilIdle();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        host.RunUntilIdle();

        errors.ShouldBe(1);
        VisibleText(host.Container).ShouldBe("previous");
        events.ShouldBe(["pending"]);
        renderer.Render(null, host.Container);
    }

    private static TaskCompletionSource<AsynchronousComponentTarget> NewLoad() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Dictionary<string, ComponentEventListener> Listeners(
        List<string> events,
        string prefix = "") =>
        new(StringComparer.Ordinal)
        {
            ["pending"] = arguments =>
            {
                arguments.ShouldBeEmpty();
                events.Add(prefix + "pending");
            },
            ["fallback"] = arguments =>
            {
                arguments.ShouldBeEmpty();
                events.Add(prefix + "fallback");
            },
            ["resolve"] = arguments =>
            {
                arguments.ShouldBeEmpty();
                events.Add(prefix + "resolve");
            },
        };

    private static AsynchronousComponentDefinition Define<TWrapper>(
        TaskCompletionSource<AsynchronousComponentTarget> load)
        where TWrapper : class, IComponent =>
        AsynchronousComponents.Define<TWrapper>(_ => load.Task);

    private static ComponentNode Request(
        AsynchronousComponentDefinition definition,
        string message) =>
        definition.CreateComponent(
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = message }));

    private static SuspenseNode Suspense(
        VirtualNode content,
        VirtualNode fallback,
        int? timeout = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null) =>
        new(
            new ComponentInvocation(
                arguments: timeout.HasValue
                    ? new Dictionary<string, object?> { ["timeout"] = timeout.Value }
                    : null,
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => content,
                    ["fallback"] = _ => fallback,
                },
                listeners: listeners));

    private static ComponentFactory CreateFactory(
        params AsynchronousComponentDefinition[] definitions)
    {
        var components = new ComponentFactory();
        for (int index = 0; index < definitions.Length; index++)
        {
            components.Register(definitions[index].Registration);
        }

        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(SuspenseTargetComponent)),
                new ComponentContract(
                    parameters: [new ComponentParameter("message")]),
                _ => new SuspenseTargetComponent()));
        return components;
    }

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        IComponentFactory components,
        ApplicationOptions? configured = null)
    {
        ApplicationOptions options = configured ?? new ApplicationOptions();
        options.RootComponent = root;
        options.Components = components;
        return new ApplicationContext(options);
    }

    private static string VisibleText(RendererParityNode node)
    {
        if (node.Kind == RendererParityNodeKind.Text)
        {
            return node.Text ?? string.Empty;
        }

        string text = string.Empty;
        for (int index = 0; index < node.Children.Count; index++)
        {
            text = string.Concat(text, VisibleText(node.Children[index]));
        }

        return text;
    }

    private sealed class FirstWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class SecondWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class SuspenseTargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode(
                context.Bindings.Parameters.TryGetValue(
                    "message",
                    out object? message)
                        ? (string?)message ?? "resolved"
                        : "resolved");
    }
}
