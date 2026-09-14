using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Core.Tests;

public sealed class AsynchronousComponentsTests
{
    [Fact]
    public void Definition_ExplicitRegistration_ActivatesFreshWrappersWithoutReflection()
    {
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                _ => Task.FromResult(AsynchronousComponentTarget.From<TargetComponent>()),
                "deferred-target");

        definition.ComponentType.ShouldBe(typeof(WrapperIdentityComponent));
        definition.Reference.ShouldBe(ComponentReference.ForName("deferred-target"));
        definition.Registration.Reference.ShouldBe(definition.Reference);
        IComponent first = definition.Registration.Activator(null);
        IComponent second = definition.Registration.Activator(null);
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void Definition_HydrationDefault_CopiedUnlessInvocationOverridesIt()
    {
        HydrationStrategy defaultStrategy = HydrationStrategy.OnIdle(100);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => Task.FromResult(
                        AsynchronousComponentTarget.From<TargetComponent>()),
                    HydrationStrategy = defaultStrategy,
                });

        ComponentNode defaultNode = definition.CreateComponent();
        HydrationStrategy explicitStrategy = HydrationStrategy.OnVisible("20px");
        ComponentNode explicitNode = definition.CreateComponent(
            new ComponentInvocation(hydrationStrategy: explicitStrategy));

        defaultNode.Invocation.HydrationStrategy.ShouldBeSameAs(defaultStrategy);
        explicitNode.Invocation.HydrationStrategy.ShouldBeSameAs(explicitStrategy);
    }

    [Fact]
    public async Task ConcurrentMounts_SharedSuccessfulLoad_ProduceFreshTargetNodesWithRawInvocation()
    {
        int loadRuns = 0;
        TaskCompletionSource loaderStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<AsynchronousComponentTarget> loaderCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loadRuns++;
                        loaderStarted.TrySetResult();
                        return loaderCompletion.Task;
                    },
                    Delay = 0,
                });
        ComponentFactory components = new();
        components.Register(definition.Registration);
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?> { ["value"] = 42 });
        ComponentNode requestNode = definition.CreateComponent(invocation);

        Task<IComponentRenderScope> firstRender;
        Task<IComponentRenderScope> secondRender;
        using (TestSynchronizationContext synchronizationContext = TestSynchronizationContext.Install())
        using (TestSchedulerPump pump = TestSchedulerPump.Install(synchronizationContext))
        {
            // [V01.01.03.20]: shared wrapper state settles on one execution flow before
            // awaiting the server host's independent render leases.
            firstRender = host.RenderAsync(new ComponentRenderRequest(requestNode)).AsTask();
            secondRender = host.RenderAsync(new ComponentRenderRequest(requestNode)).AsTask();
            loaderStarted.Task.IsCompletedSuccessfully.ShouldBeTrue();
            loaderCompletion.SetResult(AsynchronousComponentTarget.From<TargetComponent>());
            pump.RunUntilIdle();
        }

        await using IComponentRenderScope firstScope = await firstRender;
        await using IComponentRenderScope secondScope = await secondRender;
        ComponentNode firstTarget = firstScope.Tree.ShouldBeOfType<ComponentNode>();
        ComponentNode secondTarget = secondScope.Tree.ShouldBeOfType<ComponentNode>();

        loadRuns.ShouldBe(1);
        firstTarget.ShouldNotBeSameAs(secondTarget);
        firstTarget.Component.ShouldBe(ComponentReference.ForType(typeof(TargetComponent)));
        secondTarget.Component.ShouldBe(ComponentReference.ForType(typeof(TargetComponent)));
        firstTarget.Invocation.ShouldBeSameAs(invocation);
        secondTarget.Invocation.ShouldBeSameAs(invocation);
    }

    [Fact]
    public void Renderer_MountReference_TracksResolvedExposedSurfaceAndClearsOnUnmount()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        object exposed = new();
        List<object?> firstValues = [];
        List<object?> secondValues = [];
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                _ => Task.FromResult(
                    AsynchronousComponentTarget.From<ExposingTargetComponent>()));
        var components = new ComponentFactory();
        components.Register(definition.Registration);
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(ExposingTargetComponent)),
                new ComponentContract(),
                _ => new ExposingTargetComponent(exposed)));
        ComponentNode initial = definition.CreateComponent(
            mountReference: firstValues.Add);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = initial,
                Components = components,
            });

        renderer.Render(initial, host.Container, application);

        firstValues.ShouldBe([exposed]);

        ComponentNode next = definition.CreateComponent(
            mountReference: secondValues.Add);
        renderer.Render(next, host.Container);

        firstValues.ShouldBe([exposed, null]);
        secondValues.ShouldBe([exposed]);

        renderer.Render(null, host.Container);

        secondValues.ShouldBe([exposed, null]);
    }

    [Fact]
    public void Renderer_LoadingDelayAndResolution_AdvanceThroughDistinctPresentations()
    {
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    LoadingComponent = _ => new TextNode("loading"),
                    Delay = 20,
                });
        ComponentNode request = definition.CreateComponent(
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = "resolved" }));
        ComponentFactory components = CreateAsynchronousFactory(definition);
        ApplicationContext application = CreateApplication(request, components);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(request, host.Container, application);

        host.Container.DescendantText.ShouldBe(string.Empty);
        clock.Advance(TimeSpan.FromMilliseconds(19));
        host.RunUntilIdle();
        host.Container.DescendantText.ShouldBe(string.Empty);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        host.RunUntilIdle();
        host.Container.DescendantText.ShouldBe("loading");

        load.SetResult(AsynchronousComponentTarget.From<ParameterTargetComponent>());
        host.RunUntilIdle();

        host.Container.DescendantText.ShouldBe("resolved");
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_TimeoutWithErrorPresentation_RoutesAncestorAndApplicationExactlyOnce()
    {
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        List<string> captured = [];
        List<string> handled = [];
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    ErrorComponent = error => new TextNode(error.Message),
                    Timeout = 20,
                });
        ComponentReference parentReference = ComponentReference.ForType(
            typeof(ErrorCapturingParentComponent));
        var root = new ComponentNode(parentReference);
        ComponentFactory components = CreateAsynchronousFactory(definition);
        components.Register(
            new ComponentRegistration(
                parentReference,
                new ComponentContract(),
                _ => new ErrorCapturingParentComponent(definition, captured)));
        ApplicationContext application = CreateApplication(
            root,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, information) =>
                {
                    handled.Add($"{information}:{error.Message}");
                },
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        clock.Advance(TimeSpan.FromMilliseconds(19));
        host.RunUntilIdle();
        host.Container.DescendantText.ShouldBe(string.Empty);
        captured.ShouldBeEmpty();
        handled.ShouldBeEmpty();

        clock.Advance(TimeSpan.FromMilliseconds(1));
        host.RunUntilIdle();
        host.Container.DescendantText.ShouldBe(
            "Asynchronous component timed out after 20ms.");
        captured.Count.ShouldBe(1);
        handled.Count.ShouldBe(1);

        clock.Advance(TimeSpan.FromMilliseconds(40));
        host.RunUntilIdle();

        host.Container.DescendantText.ShouldBe(
            "Asynchronous component timed out after 20ms.");
        captured.ShouldBe(
        [
            "asynchronous component loader:Asynchronous component timed out after 20ms.",
        ]);
        handled.ShouldBe(
        [
            "asynchronous component loader:Asynchronous component timed out after 20ms.",
        ]);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_TimeoutThenLoaderFailure_RoutesOnlyTheFirstFailure()
    {
        using var host = new RendererParityHost();
        var clock = new ManualTimeProvider();
        using IDisposable clockRegistration = Scheduler.UseTimeProvider(clock);
        List<string> handled = [];
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    ErrorComponent = error => new TextNode(error.Message),
                    Timeout = 20,
                });
        ComponentNode request = definition.CreateComponent();
        ComponentFactory components = CreateAsynchronousFactory(definition);
        ApplicationContext application = CreateApplication(
            request,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, _) =>
                {
                    handled.Add(error.Message);
                },
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(request, host.Container, application);
        clock.Advance(TimeSpan.FromMilliseconds(19));
        host.RunUntilIdle();
        handled.ShouldBeEmpty();
        host.Container.DescendantText.ShouldBe(string.Empty);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        host.RunUntilIdle();
        handled.ShouldBe(["Asynchronous component timed out after 20ms."]);
        host.Container.DescendantText.ShouldBe(
            "Asynchronous component timed out after 20ms.");

        load.SetException(new InvalidOperationException("late loader failure"));
        host.RunUntilIdle();
        clock.Advance(TimeSpan.FromMilliseconds(50));
        host.RunUntilIdle();

        handled.ShouldBe(["Asynchronous component timed out after 20ms."]);
        host.Container.DescendantText.ShouldBe(
            "Asynchronous component timed out after 20ms.");
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_LoaderFailureWithErrorPresentation_RoutesOnceAndRendersFailure()
    {
        using var host = new RendererParityHost();
        int handledErrors = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => Task.FromException<AsynchronousComponentTarget>(
                        new InvalidOperationException("failed")),
                    ErrorComponent = error => new TextNode(error.Message),
                    Delay = 0,
                });
        ComponentNode request = definition.CreateComponent();
        ComponentFactory components = CreateAsynchronousFactory(definition);
        ApplicationContext application = CreateApplication(
            request,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (_, _, _) => handledErrors++,
            });

        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(request, host.Container, application);

        host.Container.DescendantText.ShouldBe("failed");
        handledErrors.ShouldBe(1);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_LoaderFailureWithoutPresentation_RoutesOnceAndKeepsEmptyTree()
    {
        using var host = new RendererParityHost();
        List<Exception> handled = [];
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => Task.FromException<AsynchronousComponentTarget>(
                        new InvalidOperationException("unhandled-load")),
                    Delay = 0,
                });
        ComponentNode request = definition.CreateComponent();
        ComponentFactory components = CreateAsynchronousFactory(definition);
        ApplicationContext application = CreateApplication(
            request,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, _) => handled.Add(error),
            });

        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(request, host.Container, application);

        handled.ShouldHaveSingleItem()
            .ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("unhandled-load");
        host.Container.DescendantText.ShouldBe(string.Empty);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_RetryPolicy_UsesIncrementingAttemptsUntilSuccess()
    {
        using var host = new RendererParityHost();
        int loaderRuns = 0;
        List<int> attempts = [];
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loaderRuns++;
                        return loaderRuns < 3
                            ? Task.FromException<AsynchronousComponentTarget>(
                                new InvalidOperationException($"failure-{loaderRuns}"))
                            : Task.FromResult(
                                AsynchronousComponentTarget.From<ParameterTargetComponent>());
                    },
                    OnError = (_, retry, _, attempt) =>
                    {
                        attempts.Add(attempt);
                        retry();
                    },
                });
        ComponentNode request = definition.CreateComponent(
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = "retried" }));
        ComponentFactory components = CreateAsynchronousFactory(definition);

        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            request,
            host.Container,
            CreateApplication(request, components));

        host.Container.DescendantText.ShouldBe("retried");
        loaderRuns.ShouldBe(3);
        attempts.ShouldBe([1, 2]);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_FailPolicy_SettlesWithoutRetry()
    {
        using var host = new RendererParityHost();
        int loaderRuns = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loaderRuns++;
                        return Task.FromException<AsynchronousComponentTarget>(
                            new InvalidOperationException("not-retriable"));
                    },
                    ErrorComponent = error => new TextNode(error.Message),
                    OnError = (_, _, fail, _) => fail(),
                    Delay = 0,
                });
        ComponentNode request = definition.CreateComponent();
        ComponentFactory components = CreateAsynchronousFactory(definition);

        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            request,
            host.Container,
            CreateApplication(request, components));

        host.Container.DescendantText.ShouldBe("not-retriable");
        loaderRuns.ShouldBe(1);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
    }

    [Fact]
    public void Renderer_LastConsumerCancellation_AllowsFreshLoadAfterRestart()
    {
        using var host = new RendererParityHost();
        int loaderRuns = 0;
        int cancellations = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<WrapperIdentityComponent>(
                new AsynchronousComponentOptions
                {
                    Loader = cancellationToken =>
                    {
                        loaderRuns++;
                        TaskCompletionSource<AsynchronousComponentTarget> completion = new(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        // The load state's token source owns this registration until the task settles.
                        _ = cancellationToken.Register(
                            () =>
                            {
                                cancellations++;
                                completion.TrySetCanceled(cancellationToken);
                            });
                        return completion.Task;
                    },
                    Delay = 0,
                });
        FragmentNode both = new(
        [
            definition.CreateComponent(key: "one"),
            definition.CreateComponent(key: "two"),
        ]);
        FragmentNode one = new(
        [
            definition.CreateComponent(key: "two"),
        ]);
        ComponentFactory components = CreateAsynchronousFactory(definition);
        ApplicationContext application = CreateApplication(both, components);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(both, host.Container, application);
        renderer.Render(one, host.Container);

        loaderRuns.ShouldBe(1);
        cancellations.ShouldBe(0);

        renderer.Render(null, host.Container);
        host.RunUntilIdle();

        cancellations.ShouldBe(1);

        renderer.Render(one, host.Container, application);

        loaderRuns.ShouldBe(2);
        renderer.Render(null, host.Container);
        host.RunUntilIdle();
        cancellations.ShouldBe(2);
    }

    private static ComponentFactory CreateAsynchronousFactory(
        AsynchronousComponentDefinition definition)
    {
        var components = new ComponentFactory();
        components.Register(definition.Registration);
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(ParameterTargetComponent)),
                new ComponentContract(
                    parameters: [new ComponentParameter("message")]),
                _ => new ParameterTargetComponent()));
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

    private sealed class TargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => new TextNode("target");
    }

    private sealed class ExposingTargetComponent : IComponent
    {
        private readonly object _exposed;

        internal ExposingTargetComponent(object exposed)
        {
            _exposed = exposed;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Expose(_exposed);
            return static _ => new TextNode("target");
        }
    }

    private sealed class ParameterTargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode(
                context.Bindings.Parameters.TryGetValue(
                    "message",
                    out object? message)
                        ? (string?)message ?? "target"
                        : "target");
    }

    private sealed class ErrorCapturingParentComponent : IComponent
    {
        private readonly List<string> _captured;
        private readonly AsynchronousComponentDefinition _definition;

        internal ErrorCapturingParentComponent(
            AsynchronousComponentDefinition definition,
            List<string> captured)
        {
            _definition = definition;
            _captured = captured;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnErrorCaptured(
                (error, _, information) =>
                {
                    _captured.Add($"{information}:{error.Message}");
                    return true;
                });
            return _ => _definition.CreateComponent();
        }
    }

    private sealed class WrapperIdentityComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }
}
