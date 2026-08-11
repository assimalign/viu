using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

/// <summary>Pins deterministic host triggers for [V01.01.07.03.01].</summary>
public sealed class LazyHydrationTests
{
    [Fact]
    public void TriggerIdle_AdoptedComponent_ActivatesOnPostFlush()
    {
        AssertDeferredActivation(
            HydrationStrategy.OnIdle(500),
            static triggers => triggers.TriggerIdle());
    }

    [Fact]
    public void TriggerVisible_AdoptedComponent_ActivatesOnPostFlush()
    {
        AssertDeferredActivation(
            HydrationStrategy.OnVisible("100px"),
            static triggers => triggers.TriggerVisible());
    }

    [Fact]
    public void TriggerMediaQuery_AdoptedComponent_ActivatesOnPostFlush()
    {
        AssertDeferredActivation(
            HydrationStrategy.OnMediaQuery("(min-width: 60rem)"),
            static triggers => triggers.TriggerMediaQuery("(min-width: 60rem)"));
    }

    [Fact]
    public void TriggerInteraction_AdoptedComponent_ReplaysAfterActivation()
    {
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using Scenario scenario = CreateScenario(
            HydrationStrategy.OnInteraction("click", "keydown"),
            triggers);

        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);
        triggers.TriggerInteraction("click");

        scenario.Component.SetupCount.ShouldBe(0);
        triggers.ReplayedInteractionCount.ShouldBe(0);
        pump.RunUntilIdle();
        scenario.Component.SetupCount.ShouldBe(1);
        triggers.ReplayedInteractionCount.ShouldBe(1);
        triggers.CompletedCount.ShouldBe(1);
        scenario.Renderer.Render(null, scenario.Container);
        Scheduler.Reset();
    }

    [Fact]
    public void RenderNull_BeforeTrigger_CancelsActivationAndHostRegistration()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<object?> referenceValues = [];
        using Scenario scenario = CreateScenario(
            HydrationStrategy.OnIdle(),
            triggers,
            referenceValues.Add);
        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);

        scenario.Renderer.Render(null, scenario.Container);

        triggers.PendingCount.ShouldBe(0);
        Should.Throw<InvalidOperationException>(triggers.TriggerIdle);
        pump.RunUntilIdle();
        scenario.Component.SetupCount.ShouldBe(0);
        referenceValues.ShouldBeEmpty();
        Scheduler.Reset();
    }

    [Fact]
    public void TriggerThenRenderNull_BeforePostFlush_CancelsQueuedActivation()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using Scenario scenario = CreateScenario(HydrationStrategy.OnIdle(), triggers);
        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);
        triggers.TriggerIdle();

        scenario.Renderer.Render(null, scenario.Container);
        pump.RunUntilIdle();

        scenario.Component.SetupCount.ShouldBe(0);
        triggers.CompletedCount.ShouldBe(0);
        Scheduler.Reset();
    }

    [Fact]
    public void PatchDormantStrategyData_ReplacesTheHostRegistration()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using Scenario scenario = CreateScenario(
            HydrationStrategy.OnMediaQuery("(width < 40rem)"),
            triggers);
        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);
        ComponentNode next = new(
            scenario.Root.Component,
            new ComponentInvocation(
                hydrationStrategy: HydrationStrategy.OnMediaQuery("(width >= 40rem)")));

        scenario.Renderer.Render(next, scenario.Container);

        Should.Throw<InvalidOperationException>(
            () => triggers.TriggerMediaQuery("(width < 40rem)"));
        triggers.TriggerMediaQuery("(width >= 40rem)");
        pump.RunUntilIdle();
        scenario.Component.SetupCount.ShouldBe(1);
        scenario.Renderer.Render(null, scenario.Container);
        Scheduler.Reset();
    }

    [Fact]
    public void ActivatedLazyMountReference_PublishesAndClearsExactlyOnce()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<object?> values = [];
        using Scenario scenario = CreateScenario(
            HydrationStrategy.OnVisible(),
            triggers,
            values.Add);
        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);

        triggers.TriggerVisible();
        pump.RunUntilIdle();
        scenario.Renderer.Render(null, scenario.Container);

        values.Count.ShouldBe(2);
        values[0].ShouldBeSameAs(scenario.Component);
        values[1].ShouldBeNull();
        Scheduler.Reset();
    }

    [Fact]
    public async Task AsynchronousDefinition_PendingLoader_PreservesMarkupUntilOneTriggerCompletes()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<AsynchronousWrapperIdentity>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    Delay = 0,
                    HydrationStrategy = HydrationStrategy.OnIdle(),
                });
        LazyTargetComponent target = new();
        ComponentFactory components = new();
        components.Register(definition.Registration);
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(LazyTargetComponent)),
                new ComponentContract(),
                _ => target));
        ComponentNode root = definition.CreateComponent();
        ApplicationContext application = CreateApplication(root, components);
        using TestRenderer renderer = CreateRenderer(triggers);
        TestElement container = TestServerMarkup.Parse(
            Marker(HydrationStrategyKind.Idle)
                + "<button>ready</button>"
                + HydrationMarkers.LazyHydrationEnd);
        TestNode adopted = container.Children[1];

        renderer.Hydrate(root, container, application);
        triggers.TriggerIdle();
        pump.RunUntilIdle();

        target.SetupCount.ShouldBe(0);
        container.Children[1].ShouldBeSameAs(adopted);

        load.SetResult(AsynchronousComponentTarget.From<LazyTargetComponent>());
        for (int attempt = 0; attempt < 5000 && pump.PendingFlushCount == 0; attempt++)
        {
            await Task.Yield();
        }
        pump.PendingFlushCount.ShouldBeGreaterThan(0);
        pump.RunUntilIdle();

        target.SetupCount.ShouldBe(1);
        triggers.CompletedCount.ShouldBe(1);
        container.Children[1].ShouldBeSameAs(adopted);
        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public async Task AsynchronousDefinition_NeverCompletingLoader_TimeoutActivatesErrorPresentation()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Exception> routed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<AsynchronousWrapperIdentity>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    Delay = 0,
                    Timeout = 20,
                    ErrorComponent = error => new ElementNode(
                        new QualifiedName("p"),
                        children: [new TextNode(error.Message)]),
                    HydrationStrategy = HydrationStrategy.OnIdle(),
                });
        ComponentFactory components = new();
        components.Register(definition.Registration);
        ComponentNode root = definition.CreateComponent();
        ApplicationContext application = new(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
                ErrorHandler = (error, _, _) => routed.TrySetResult(error),
            });
        using TestRenderer renderer = CreateRenderer(triggers);
        TestElement container = TestServerMarkup.Parse(
            Marker(HydrationStrategyKind.Idle)
                + "<p>server</p>"
                + HydrationMarkers.LazyHydrationEnd);
        TestNode adopted = container.Children[1];

        renderer.Hydrate(root, container, application);
        triggers.TriggerIdle();
        pump.RunUntilIdle();

        load.Task.IsCompleted.ShouldBeFalse();
        container.Children[1].ShouldBeSameAs(adopted);

        Exception routedError = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (int attempt = 0; attempt < 5000 && pump.PendingFlushCount == 0; attempt++)
        {
            await Task.Yield();
        }
        pump.PendingFlushCount.ShouldBeGreaterThan(0);
        pump.RunUntilIdle();

        routedError.ShouldBeOfType<TimeoutException>();
        TestElement errorPresentation = container.Children[1].ShouldBeOfType<TestElement>();
        errorPresentation.ShouldBeSameAs(adopted);
        errorPresentation.Tag.ShouldBe("p");
        errorPresentation.Children.Count.ShouldBe(1);
        errorPresentation.Children[0]
            .ShouldBeOfType<TestText>()
            .Text.ShouldBe("Asynchronous component timed out after 20ms.");
        load.Task.IsCompleted.ShouldBeFalse();
        triggers.CompletedCount.ShouldBe(1);
        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public void NestedLazyBoundaries_OuterActivationSchedulesInnerIndependently()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using TestRenderer renderer = CreateRenderer(triggers);
        CountingComponent inner = new(
            () => new ElementNode(
                new QualifiedName("span"),
                children: [new TextNode("ready")]));
        ComponentFactory components = new();
        ComponentNode innerNode = Register(
            components,
            "inner",
            inner,
            HydrationStrategy.OnVisible());
        CountingComponent outer = new(() => innerNode);
        ComponentNode root = Register(
            components,
            "outer",
            outer,
            HydrationStrategy.OnIdle());
        ApplicationContext application = CreateApplication(root, components);
        TestElement container = TestServerMarkup.Parse(
            Marker(HydrationStrategyKind.Idle)
                + Marker(HydrationStrategyKind.Visible)
                + "<span>ready</span>"
                + HydrationMarkers.LazyHydrationEnd
                + HydrationMarkers.LazyHydrationEnd);

        renderer.Hydrate(root, container, application);
        triggers.PendingCount.ShouldBe(1);
        outer.SetupCount.ShouldBe(0);
        inner.SetupCount.ShouldBe(0);

        triggers.TriggerIdle();
        pump.RunUntilIdle();
        outer.SetupCount.ShouldBe(1);
        inner.SetupCount.ShouldBe(0);
        triggers.PendingCount.ShouldBe(1);

        triggers.TriggerVisible();
        pump.RunUntilIdle();
        inner.SetupCount.ShouldBe(1);
        triggers.CompletedCount.ShouldBe(2);
        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public void EagerParentWithLazyChild_ActivatesOnlyTheParentDuringInitialWalk()
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using TestRenderer renderer = CreateRenderer(triggers);
        CountingComponent child = new(
            () => new ElementNode(
                new QualifiedName("strong"),
                children: [new TextNode("child")]));
        ComponentFactory components = new();
        ComponentNode childNode = Register(
            components,
            "child",
            child,
            HydrationStrategy.OnVisible());
        CountingComponent parent = new(() => childNode);
        ComponentNode root = Register(components, "parent", parent, null);
        ApplicationContext application = CreateApplication(root, components);
        TestElement container = TestServerMarkup.Parse(
            Marker(HydrationStrategyKind.Visible)
                + "<strong>child</strong>"
                + HydrationMarkers.LazyHydrationEnd);

        renderer.Hydrate(root, container, application);

        parent.SetupCount.ShouldBe(1);
        child.SetupCount.ShouldBe(0);
        triggers.PendingCount.ShouldBe(1);
        triggers.TriggerVisible();
        pump.RunUntilIdle();
        child.SetupCount.ShouldBe(1);
        renderer.Render(null, container);
        Scheduler.Reset();
    }

    private static void AssertDeferredActivation(
        HydrationStrategy strategy,
        Action<TestHydrationTriggers> trigger)
    {
        Scheduler.Reset();
        TestHydrationTriggers triggers = new();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        using Scenario scenario = CreateScenario(strategy, triggers);
        TestNode adopted = scenario.Container.Children[1];

        scenario.Renderer.Hydrate(
            scenario.Root,
            scenario.Container,
            scenario.Application);

        scenario.Component.SetupCount.ShouldBe(0);
        triggers.PendingCount.ShouldBe(1);
        trigger(triggers);
        scenario.Component.SetupCount.ShouldBe(0);
        pump.PendingFlushCount.ShouldBe(1);
        pump.RunUntilIdle();

        scenario.Component.SetupCount.ShouldBe(1);
        triggers.PendingCount.ShouldBe(0);
        triggers.CompletedCount.ShouldBe(1);
        scenario.Container.Children[1].ShouldBeSameAs(adopted);
        scenario.Renderer.Render(null, scenario.Container);
        Scheduler.Reset();
    }

    private static Scenario CreateScenario(
        HydrationStrategy strategy,
        TestHydrationTriggers triggers,
        MountReference? mountReference = null)
    {
        TestRenderer renderer = CreateRenderer(triggers);
        CountingComponent component = new(
            () => new ElementNode(
                new QualifiedName("button"),
                children: [new TextNode("ready")]));
        ComponentFactory components = new();
        ComponentNode root = Register(
            components,
            "target",
            component,
            strategy,
            mountReference);
        ApplicationContext application = CreateApplication(root, components);
        TestElement container = TestServerMarkup.Parse(
            Marker(strategy.Kind)
                + "<button>ready</button>"
                + HydrationMarkers.LazyHydrationEnd);
        return new Scenario(renderer, container, root, application, component);
    }

    private static TestRenderer CreateRenderer(TestHydrationTriggers triggers) => new(
        new TestRendererOptions
        {
            SnapshotSemantics = true,
            HydrationTriggers = triggers,
        });

    private static ComponentNode Register(
        ComponentFactory components,
        string name,
        CountingComponent component,
        HydrationStrategy? strategy,
        MountReference? mountReference = null)
    {
        ComponentReference reference = ComponentReference.ForName(name);
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(displayName: name),
                _ => component));
        return new ComponentNode(
            reference,
            new ComponentInvocation(hydrationStrategy: strategy),
            mountReference: mountReference);
    }

    private static ApplicationContext CreateApplication(
        ComponentNode root,
        ComponentFactory components) => new(
        new ApplicationOptions
        {
            RootComponent = root,
            Components = components,
        });

    private static string Marker(HydrationStrategyKind kind) =>
        HydrationMarkers.GetLazyHydrationStart(kind);

    private sealed record Scenario(
        TestRenderer Renderer,
        TestElement Container,
        ComponentNode Root,
        ApplicationContext Application,
        CountingComponent Component) : IDisposable
    {
        public void Dispose() => Renderer.Dispose();
    }

    private sealed class CountingComponent : IComponent
    {
        private readonly Func<VirtualNode> _render;

        internal CountingComponent(Func<VirtualNode> render)
        {
            _render = render;
        }

        internal int SetupCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            SetupCount++;
            return _ => _render();
        }
    }

    private sealed class LazyTargetComponent : IComponent
    {
        internal int SetupCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            SetupCount++;
            return static _ => new ElementNode(
                new QualifiedName("button"),
                children: [new TextNode("ready")]);
        }
    }

    private sealed class AsynchronousWrapperIdentity : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => static _ => null;
    }
}
