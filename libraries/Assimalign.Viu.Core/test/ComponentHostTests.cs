using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

public sealed class ComponentHostTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task RenderAsync_CompilerContract_UsesExactRenderCacheSize(int renderCacheSize)
    {
        string actual = await RenderCacheSizeAsync(
            new ComponentContract(renderCacheSize: renderCacheSize));

        actual.ShouldBe(renderCacheSize.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task RenderAsync_LegacyContract_UsesCompatibilityRenderCacheSize()
    {
        string actual = await RenderCacheSizeAsync(new ComponentContract());

        actual.ShouldBe("64");
    }

    [Fact]
    public async Task RenderAsync_RawInvocation_ProducesNormalizedBindingsAndFreshTree()
    {
        var reference = ComponentReference.ForType(typeof(GreetingComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(parameters: new[] { new ComponentParameter("name") }),
                _ => new GreetingComponent()));
        var host = new ComponentHost(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["name"] = "Viu" });

        await using var scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));

        var output = scope.Tree.ShouldBeOfType<TextNode>();
        output.Text.ShouldBe("Hello Viu");
        scope.Context.Bindings.Parameters["name"].ShouldBe("Viu");
        scope.Context.Parent.ShouldBeNull();
    }

    [Fact]
    public async Task RenderAsync_NestedRequest_ParentScopeContextBecomesChildContextParent()
    {
        var reference = ComponentReference.ForType(typeof(GreetingComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(parameters: new[] { new ComponentParameter("name") }),
                _ => new GreetingComponent()));
        var host = new ComponentHost(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["name"] = "Viu" });

        await using var parent = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));
        await using var child = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation), parent));

        child.Context.Parent.ShouldBeSameAs(parent.Context);
    }

    [Fact]
    public async Task DisposeAsync_ServerRender_CancelsLifetimeBeforeScopeAndInstanceTeardown()
    {
        ComponentReference reference = ComponentReference.ForType(typeof(LifecycleComponent));
        ComponentFactory components = new();
        LifecycleProbe probe = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new LifecycleComponent(probe)));
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));

        IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));
        CancellationToken lifetimeToken = scope.Context.Lifecycle.CancellationToken;

        // A server lease aborts without client hooks and cancels before teardown [CMP-22] [SSR-5].
        await scope.DisposeAsync();

        lifetimeToken.IsCancellationRequested.ShouldBeTrue();
        probe.TeardownOrder.ShouldBe(["lifetime", "scope", "instance"]);
        probe.ClientHookRuns.ShouldBe(0);
    }

    [Fact]
    public async Task RenderAsync_CancelledServerPrefetch_AbortsCompleteComponentLifetime()
    {
        ComponentReference reference = ComponentReference.ForType(
            typeof(CancellablePrefetchComponent));
        ComponentFactory components = new();
        LifecycleProbe probe = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new CancellablePrefetchComponent(probe)));
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        using CancellationTokenSource cancellationSource = new();

        Task<IComponentRenderScope> renderTask = host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)),
            cancellationSource.Token).AsTask();
        await probe.PrefetchStarted.Task;
        cancellationSource.Cancel();

        // External render cancellation still completes the component abort path [SSR-5].
        await Should.ThrowAsync<OperationCanceledException>(async () => await renderTask);
        probe.LifetimeToken.IsCancellationRequested.ShouldBeTrue();
        probe.TeardownOrder.ShouldBe(["lifetime", "scope", "instance"]);
        probe.ClientHookRuns.ShouldBe(0);
    }

    [Fact]
    public async Task LifecycleFault_AncestorStopsPropagation_ThenRootFaultReachesTerminalHandler()
    {
        ComponentReference parentReference = ComponentReference.ForType(
            typeof(CapturingComponent));
        ComponentReference childReference = ComponentReference.ForType(
            typeof(PlainComponent));
        ComponentFactory components = new();
        List<ComponentContext?> capturedSources = [];
        List<ComponentContext?> terminalSources = [];
        components.Register(
            new ComponentRegistration(
                parentReference,
                new ComponentContract(),
                _ => new CapturingComponent(capturedSources)));
        components.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(),
                _ => new PlainComponent()));
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                errorHandler: (_, source, _) => terminalSources.Add(source)));

        await using IComponentRenderScope parent = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(parentReference)));
        await using IComponentRenderScope child = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(childReference), parent));
        child.Context.Lifecycle.OnMounted(
            () => Task.FromException(new InvalidOperationException("child")));

        // Ordinary task faults visit ancestor capture before the terminal sink [CMP-21] [CMP-23].
        child.Context.Lifecycle.InvokeMounted();
        await child.Context.Lifecycle.DrainAsync();

        capturedSources.ShouldHaveSingleItem().ShouldBeSameAs(child.Context);
        terminalSources.ShouldBeEmpty();

        parent.Context.Lifecycle.OnMounted(
            () => Task.FromException(new InvalidOperationException("root")));
        parent.Context.Lifecycle.InvokeMounted();
        await parent.Context.Lifecycle.DrainAsync();

        terminalSources.ShouldHaveSingleItem().ShouldBeSameAs(parent.Context);
    }

    [Fact]
    public async Task DisposeAsync_OrdinaryLifecycleTask_DrainsBeforeReleasingErrorRouting()
    {
        ComponentReference reference = ComponentReference.ForType(typeof(PlainComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new PlainComponent()));
        List<string> errors = [];
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                errorHandler: (exception, _, _) => errors.Add(exception.Message)));
        IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));
        TaskCompletionSource callbackCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Context.Lifecycle.OnMounted(async () =>
        {
            await callbackCompletion.Task;
            throw new InvalidOperationException("late lifecycle failure");
        });
        scope.Context.Lifecycle.InvokeMounted();

        // Lease disposal drains observers before it releases their routing hook [CMP-21].
        Task disposal = scope.DisposeAsync().AsTask();
        disposal.IsCompleted.ShouldBeFalse();
        callbackCompletion.SetResult();
        await disposal;

        errors.ShouldBe(["late lifecycle failure"]);
    }

    [Fact]
    public async Task WatchAndEventFaults_Uncaptured_RouteToTerminalHandler()
    {
        ComponentReference reference = ComponentReference.ForType(typeof(FaultRoutingComponent));
        ComponentFactory components = new();
        FaultRoutingComponent? instance = null;
        List<string> diagnostics = [];
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => instance = new FaultRoutingComponent()));
        ComponentInvocation invocation = new(
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["fail"] = _ => throw new InvalidOperationException("event"),
            });
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                errorHandler: (_, _, diagnosticInformation) =>
                    diagnostics.Add(diagnosticInformation)));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));

        // Watch and event failures share the same terminal application sink [CMP-23].
        instance.ShouldNotBeNull().Value.Value = 1;
        scope.Context.Emit("fail");

        diagnostics.ShouldBe(
            ["component watch callback", "component event listener \"fail\""]);
    }

    [Fact]
    public async Task RenderAsync_DefaultAndRequiredBindings_EvaluateAndWarnOnceForTheMount()
    {
        int defaultFactoryRuns = 0;
        List<string> warnings = [];
        ComponentReference reference = ComponentReference.ForType(typeof(GreetingComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    parameters:
                    [
                        new ComponentParameter(
                            "name",
                            defaultFactory: () =>
                            {
                                defaultFactoryRuns++;
                                return "default";
                            }),
                        new ComponentParameter("required", isRequired: true),
                    ]),
                _ => new GreetingComponent()));
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                warnHandler: warnings.Add));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));

        scope.Tree.ShouldBeOfType<TextNode>().Text.ShouldBe("Hello default");
        scope.Context.Bindings.Parameters["name"].ShouldBe("default");
        defaultFactoryRuns.ShouldBe(1);
        warnings.ShouldHaveSingleItem()
            .ShouldContain("Required parameter 'required'");
    }

    [Fact]
    public async Task Emit_ExactCamelizedOnceValidatorAndObserver_UseMountedListenerState()
    {
        ComponentReference reference = ComponentReference.ForType(typeof(ContextCaptureComponent));
        ComponentFactory components = new();
        ContextCaptureComponent? instance = null;
        int exactRuns = 0;
        int ordinaryRuns = 0;
        int onceRuns = 0;
        int validatorRuns = 0;
        List<string> warnings = [];
        List<string> observedEvents = [];
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    events:
                    [
                        new ComponentEvent("exact"),
                        new ComponentEvent(
                            "saveItem",
                            arguments =>
                            {
                                validatorRuns++;
                                return arguments.Count == 1
                                    && string.Equals(
                                        arguments[0] as string,
                                        "valid",
                                        StringComparison.Ordinal);
                            }),
                    ]),
                _ => instance = new ContextCaptureComponent()));
        ComponentInvocation invocation = new(
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["exact"] = _ => exactRuns++,
                ["saveItem"] = _ => ordinaryRuns++,
                ["saveItemOnce"] = _ => onceRuns++,
            });
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                warnHandler: warnings.Add,
                eventObserver: (_, name, _) => observedEvents.Add(name)));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));
        ComponentContext context = instance.ShouldNotBeNull().Context.ShouldNotBeNull();

        context.Emit("exact");
        context.Emit("save-item", "valid");
        context.Emit("save-item", "valid");
        context.Emit("save-item", "invalid");

        exactRuns.ShouldBe(1);
        ordinaryRuns.ShouldBe(3);
        onceRuns.ShouldBe(1);
        validatorRuns.ShouldBe(3);
        warnings.ShouldHaveSingleItem()
            .ShouldContain("Invalid arguments were emitted");
        observedEvents.ShouldBe(["exact", "save-item", "save-item", "save-item"]);
    }

    [Fact]
    public async Task RenderAsync_ServerPrefetch_CompletesBeforeTheSingleRender()
    {
        ComponentReference reference = ComponentReference.ForType(typeof(OrderedPrefetchComponent));
        ComponentFactory components = new();
        List<string> order = [];
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new OrderedPrefetchComponent(order)));
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));

        scope.Tree.ShouldBeOfType<TextNode>().Text.ShouldBe("ordered");
        order.ShouldBe(["prefetch", "render"]);
    }

    [Fact]
    public async Task RenderAsync_HandledRenderFailure_ReturnsAnEmptyTreeLease()
    {
        ComponentReference reference = ComponentReference.ForType(
            typeof(ThrowingRenderComponent));
        ComponentFactory components = new();
        List<string> diagnostics = [];
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new ThrowingRenderComponent()));
        ComponentHost host = new(
            new ComponentRuntimeOptions(
                components,
                new ImmediateWatchScheduler(),
                errorHandler: (_, _, diagnosticInformation) =>
                    diagnostics.Add(diagnosticInformation)));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));

        scope.Tree.ShouldBeNull();
        diagnostics.ShouldBe(["component render"]);
    }

    [Fact]
    public async Task RenderAsync_UnhandledRenderFailure_PropagatesAfterAbortingTheLease()
    {
        ComponentReference reference = ComponentReference.ForType(
            typeof(ThrowingLifecycleRenderComponent));
        ComponentFactory components = new();
        LifecycleProbe probe = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new ThrowingLifecycleRenderComponent(probe)));
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await host.RenderAsync(
                new ComponentRenderRequest(new ComponentNode(reference))));

        exception.Message.ShouldBe("render failure");
        probe.LifetimeToken.IsCancellationRequested.ShouldBeTrue();
        probe.TeardownOrder.ShouldBe(["lifetime", "scope", "instance"]);
        probe.ClientHookRuns.ShouldBe(0);
    }

    private sealed class GreetingComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode($"Hello {context.Bindings.Parameters["name"]}");
    }

    private sealed class CacheSizeReportingComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            frame => new TextNode(
                frame.Cache.Length.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class ContextCaptureComponent : IComponent
    {
        internal ComponentContext? Context { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            Context = context;
            return _ => new TextNode("captured");
        }
    }

    private sealed class OrderedPrefetchComponent : IComponent
    {
        private readonly List<string> _order;

        internal OrderedPrefetchComponent(List<string> order)
        {
            _order = order;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnServerPrefetch(async () =>
            {
                await Task.Yield();
                _order.Add("prefetch");
            });
            return _ =>
            {
                _order.Add("render");
                return new TextNode("ordered");
            };
        }
    }

    private sealed class PlainComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode("plain");
    }

    private sealed class ThrowingRenderComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => throw new InvalidOperationException("render failure");
    }

    private sealed class ThrowingLifecycleRenderComponent : IComponent, IDisposable
    {
        private readonly LifecycleProbe _probe;

        internal ThrowingLifecycleRenderComponent(LifecycleProbe probe)
        {
            _probe = probe;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            RegisterClientHooks(context.Lifecycle, _probe);
            _probe.LifetimeToken = context.Lifecycle.CancellationToken;
            context.Lifecycle.CancellationToken.Register(
                () => _probe.TeardownOrder.Add("lifetime"));
            Reactive.OnScopeDispose(() => _probe.TeardownOrder.Add("scope"));
            return _ => throw new InvalidOperationException("render failure");
        }

        public void Dispose() => _probe.TeardownOrder.Add("instance");
    }

    private sealed class LifecycleComponent : IComponent, IDisposable
    {
        private readonly LifecycleProbe _probe;

        internal LifecycleComponent(LifecycleProbe probe)
        {
            _probe = probe;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            RegisterClientHooks(context.Lifecycle, _probe);
            _probe.LifetimeToken = context.Lifecycle.CancellationToken;
            context.Lifecycle.CancellationToken.Register(
                () => _probe.TeardownOrder.Add("lifetime"));
            Reactive.OnScopeDispose(() => _probe.TeardownOrder.Add("scope"));
            return _ => new TextNode("lifecycle");
        }

        public void Dispose() => _probe.TeardownOrder.Add("instance");
    }

    private sealed class CancellablePrefetchComponent : IComponent, IDisposable
    {
        private readonly LifecycleProbe _probe;

        internal CancellablePrefetchComponent(LifecycleProbe probe)
        {
            _probe = probe;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            RegisterClientHooks(context.Lifecycle, _probe);
            _probe.LifetimeToken = context.Lifecycle.CancellationToken;
            context.Lifecycle.CancellationToken.Register(
                () => _probe.TeardownOrder.Add("lifetime"));
            Reactive.OnScopeDispose(() => _probe.TeardownOrder.Add("scope"));
            context.Lifecycle.OnServerPrefetch(async cancellationToken =>
            {
                _probe.PrefetchStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
            return _ => new TextNode("prefetch");
        }

        public void Dispose() => _probe.TeardownOrder.Add("instance");
    }

    private sealed class CapturingComponent : IComponent
    {
        private readonly List<ComponentContext?> _capturedSources;

        internal CapturingComponent(List<ComponentContext?> capturedSources)
        {
            _capturedSources = capturedSources;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnErrorCaptured((_, source, _) =>
            {
                _capturedSources.Add(source);
                return false;
            });
            return _ => new TextNode("parent");
        }
    }

    private sealed class FaultRoutingComponent : IComponent
    {
        internal Reference<int> Value { get; } = Reactive.Reference(0);

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Watch<int>(
                () => Value.Value,
                (_, _) => throw new InvalidOperationException("watch"));
            return _ => new TextNode("faults");
        }
    }

    private sealed class LifecycleProbe
    {
        internal int ClientHookRuns { get; set; }

        internal CancellationToken LifetimeToken { get; set; }

        internal TaskCompletionSource PrefetchStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<string> TeardownOrder { get; } = [];
    }

    private static void RegisterClientHooks(
        ComponentLifecycle lifecycle,
        LifecycleProbe probe)
    {
        lifecycle.OnBeforeMount(() => probe.ClientHookRuns++);
        lifecycle.OnMounted(() => probe.ClientHookRuns++);
        lifecycle.OnBeforeUpdate(() => probe.ClientHookRuns++);
        lifecycle.OnUpdated(() => probe.ClientHookRuns++);
        lifecycle.OnBeforeUnmount(() => probe.ClientHookRuns++);
        lifecycle.OnUnmounted(() => probe.ClientHookRuns++);
        lifecycle.OnActivated(() => probe.ClientHookRuns++);
        lifecycle.OnDeactivated(() => probe.ClientHookRuns++);
    }

    private static async Task<string> RenderCacheSizeAsync(ComponentContract contract)
    {
        ComponentReference reference = ComponentReference.ForType(
            typeof(CacheSizeReportingComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                contract,
                _ => new CacheSizeReportingComponent()));
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));

        await using IComponentRenderScope scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference)));

        return scope.Tree.ShouldBeOfType<TextNode>().Text;
    }
}
