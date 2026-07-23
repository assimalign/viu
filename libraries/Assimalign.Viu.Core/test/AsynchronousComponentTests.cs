using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins asynchronous components against Vue 3.5's loader, caching, and presentation semantics.
/// </summary>
public sealed class AsynchronousComponentTests : IDisposable
{
    private readonly TestSchedulerPump _pump;
    private readonly ManualTimeController _time;
    private readonly SynchronizationContext? _previousContext;

    public AsynchronousComponentTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _time = new ManualTimeController();
        _previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new InlineSynchronizationContext());
    }

    public void Dispose()
    {
        SuspenseBoundaryContext.Current = null;
        SynchronizationContext.SetSynchronizationContext(_previousContext);
        _time.Dispose();
        _pump.Dispose();
        Scheduler.Reset();
    }

    [Fact]
    public void Definition_LoadsOnFirstMount_CachesTargetAndUsesFreshFactoryActivation()
    {
        int loaderRuns = 0;
        int setupRuns = 0;
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ =>
                {
                    loaderRuns++;
                    return load.Task;
                });
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "ready")));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(
            request,
            definition,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                () => new ResolvedTemplate(() => setupRuns++)));

        loaderRuns.ShouldBe(0);
        renderer.Render(request, host.Root, application);
        loaderRuns.ShouldBe(1);
        host.Text(host.Root).ShouldBe(string.Empty);

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("ready");
        setupRuns.ShouldBe(1);

        renderer.Render(null, host.Root);
        renderer.Render(request, host.Root, application);
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("ready");
        loaderRuns.ShouldBe(1);
        setupRuns.ShouldBe(2);
    }

    [Fact]
    public void Loader_ConcurrentMountsShareOneRequest_ResolvedTemplatesRemainPerMount()
    {
        int loaderRuns = 0;
        int setupRuns = 0;
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ =>
                {
                    loaderRuns++;
                    return load.Task;
                });
        IComponent root = ComponentTree.Fragment(
        [
            definition.CreateComponent(
                Arguments(("message", "one")),
                key: "one"),
            definition.CreateComponent(
                Arguments(("message", "two")),
                key: "two"),
        ]);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(
            root,
            definition,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                () => new ResolvedTemplate(() => setupRuns++)));

        renderer.Render(root, host.Root, application);
        loaderRuns.ShouldBe(1);

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("onetwo");
        loaderRuns.ShouldBe(1);
        setupRuns.ShouldBe(2);
    }

    [Fact]
    public void Loader_NamedTarget_ActivatesThroughFactoryName()
    {
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => Task.FromResult(
                    new AsynchronousComponentTarget("resolved-by-name")));
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "named")));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            request,
            host.Root,
            Application(
                request,
                definition,
                new ComponentRegistration(
                    typeof(ResolvedTemplate),
                    static () => new ResolvedTemplate(),
                    "resolved-by-name")));

        host.Text(host.Root).ShouldBe("named");
    }

    [Fact]
    public void Wrapper_ForwardsArgumentsSlotsAndListenersToResolvedFactoryComponent()
    {
        int emitted = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => Task.FromResult(
                    AsynchronousComponentTarget.From<ForwardingTemplate>()));
        IReadOnlyDictionary<string, ComponentSlot> slots =
            new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
            {
                ["default"] = static _ => ComponentTree.Text("-slot"),
            };
        IReadOnlyDictionary<string, ComponentEventListener> listeners =
            new Dictionary<string, ComponentEventListener>(StringComparer.Ordinal)
            {
                ["ready"] = new ComponentEventListener(_ => emitted++),
            };
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "forwarded")),
            slots,
            listeners: listeners);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            request,
            host.Root,
            Application(
                request,
                definition,
                new ComponentRegistration(
                    typeof(ForwardingTemplate),
                    static () => new ForwardingTemplate())));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("forwarded-slot");
        emitted.ShouldBe(1);
    }

    [Fact]
    public void Wrapper_ReferenceTracksResolvedExposedSurfaceAndClearsOnUnmount()
    {
        object exposed = new();
        Reference<object?> first = Reactive.Reference<object?>(null);
        Reference<object?> second = Reactive.Reference<object?>(null);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => Task.FromResult(
                    AsynchronousComponentTarget.From<ExposingResolvedTemplate>()));
        ITemplateComponent initial = definition.CreateComponent(
            reference: TemplateReference.FromReference(first));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(
            initial,
            definition,
            new ComponentRegistration(
                typeof(ExposingResolvedTemplate),
                () => new ExposingResolvedTemplate(exposed)));

        renderer.Render(initial, host.Root, application);

        first.Value.ShouldBeSameAs(exposed);

        ITemplateComponent next = definition.CreateComponent(
            reference: TemplateReference.FromReference(second));
        renderer.Render(next, host.Root);

        first.Value.ShouldBeNull();
        second.Value.ShouldBeSameAs(exposed);

        renderer.Render(null, host.Root);

        second.Value.ShouldBeNull();
    }

    [Fact]
    public void LoadingPresentation_AppearsAfterDelay_ThenResolutionReplacesIt()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    LoadingComponent = static () => ComponentTree.Text("loading"),
                    Delay = 50,
                });
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "resolved")));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(
            request,
            definition,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                static () => new ResolvedTemplate()));

        renderer.Render(request, host.Root, application);
        host.Text(host.Root).ShouldBe(string.Empty);

        _time.Advance(49);
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe(string.Empty);

        _time.Advance(1);
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("loading");

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("resolved");
    }

    [Fact]
    public void Timeout_RendersErrorPresentationAndRoutesErrorOnce()
    {
        int handledErrors = 0;
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    ErrorComponent = error => ComponentTree.Text(error.Message),
                    Timeout = 100,
                });
        ITemplateComponent request = definition.CreateComponent();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(request, definition);
        application.ErrorHandler = (_, _, _) => handledErrors++;

        renderer.Render(request, host.Root, application);
        _time.Advance(100);
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe(
            "Asynchronous component timed out after 100ms.");
        handledErrors.ShouldBe(1);

        renderer.Render(null, host.Root);
    }

    [Fact]
    public void LoaderFailure_ErrorPresentationDoesNotRequireApplicationErrorHandler()
    {
        int loaderRuns = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loaderRuns++;
                        return Task.FromException<AsynchronousComponentTarget>(
                            new InvalidOperationException("failed"));
                    },
                    ErrorComponent = error => ComponentTree.Text(error.Message),
                    Delay = 0,
                });
        ITemplateComponent request = definition.CreateComponent();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            request,
            host.Root,
            Application(request, definition));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("failed");
        loaderRuns.ShouldBe(1);
    }

    [Fact]
    public void LoaderFailure_WithoutErrorPresentationRoutesToApplicationHandler()
    {
        Exception? handled = null;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => Task.FromException<AsynchronousComponentTarget>(
                    new InvalidOperationException("unhandled-load")));
        ITemplateComponent request = definition.CreateComponent();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(request, definition);
        application.ErrorHandler = (error, _, _) => handled = error;

        renderer.Render(request, host.Root, application);
        _pump.RunUntilIdle();

        handled.ShouldBeOfType<InvalidOperationException>();
        handled!.Message.ShouldBe("unhandled-load");
        host.Text(host.Root).ShouldBe(string.Empty);
    }

    [Fact]
    public void ErrorHandler_RetryUsesIncrementingAttemptsUntilSuccess()
    {
        int loaderRuns = 0;
        List<int> attempts = [];
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loaderRuns++;
                        return loaderRuns < 3
                            ? Task.FromException<AsynchronousComponentTarget>(
                                new InvalidOperationException($"failure-{loaderRuns}"))
                            : Task.FromResult(
                                AsynchronousComponentTarget.From<ResolvedTemplate>());
                    },
                    OnError = (_, retry, _, attempt) =>
                    {
                        attempts.Add(attempt);
                        retry();
                    },
                });
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "retried")));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            request,
            host.Root,
            Application(
                request,
                definition,
                new ComponentRegistration(
                    typeof(ResolvedTemplate),
                    static () => new ResolvedTemplate())));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("retried");
        loaderRuns.ShouldBe(3);
        attempts.ShouldBe([1, 2]);
    }

    [Fact]
    public void ErrorHandler_FailSettlesWithoutRetry()
    {
        int loaderRuns = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ =>
                    {
                        loaderRuns++;
                        return Task.FromException<AsynchronousComponentTarget>(
                            new InvalidOperationException("not-retriable"));
                    },
                    ErrorComponent = error => ComponentTree.Text(error.Message),
                    OnError = (_, _, fail, _) => fail(),
                });
        ITemplateComponent request = definition.CreateComponent();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(
            request,
            host.Root,
            Application(request, definition));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("not-retriable");
        loaderRuns.ShouldBe(1);
    }

    [Fact]
    public void Cancellation_LastConcurrentConsumerUnmounts_CancelsSharedLoadAndAllowsRestart()
    {
        int loaderRuns = 0;
        int cancellations = 0;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                async cancellationToken =>
                {
                    loaderRuns++;
                    _ = cancellationToken.Register(() => cancellations++);
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return AsynchronousComponentTarget.From<ResolvedTemplate>();
                });
        IComponent both = ComponentTree.Fragment(
        [
            definition.CreateComponent(key: "one"),
            definition.CreateComponent(key: "two"),
        ]);
        IComponent one = ComponentTree.Fragment(
        [
            definition.CreateComponent(key: "two"),
        ]);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        ApplicationContext application = Application(both, definition);

        renderer.Render(both, host.Root, application);
        loaderRuns.ShouldBe(1);

        renderer.Render(one, host.Root);
        cancellations.ShouldBe(0);

        renderer.Render(null, host.Root);
        cancellations.ShouldBe(1);

        renderer.Render(one, host.Root, application);
        loaderRuns.ShouldBe(2);
        renderer.Render(null, host.Root);
        cancellations.ShouldBe(2);
    }

    [Fact]
    public void SuspensibleBoundary_RegistersSharedLoadAndSuppressesLocalLoadingPresentation()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        RecordingSuspenseBoundary boundary = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                new AsynchronousComponentOptions
                {
                    Loader = _ => load.Task,
                    LoadingComponent = static () => ComponentTree.Text("loading"),
                    Delay = 0,
                    Suspensible = true,
                });
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "resolved")));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        SuspenseBoundaryContext.Current = boundary;
        try
        {
            renderer.Render(
                request,
                host.Root,
                Application(
                    request,
                    definition,
                    new ComponentRegistration(
                        typeof(ResolvedTemplate),
                        static () => new ResolvedTemplate())));
        }
        finally
        {
            SuspenseBoundaryContext.Current = null;
        }

        boundary.Dependencies.Count.ShouldBe(1);
        host.Text(host.Root).ShouldBe(string.Empty);

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("resolved");
    }

    [Fact]
    public void SuspenseBoundary_InheritedContextRegistersChildMountedAfterInitialRender()
    {
        Reference<bool> showChild = Reactive.Reference(false);
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        RecordingSuspenseBoundary boundary = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => load.Task);
        ITemplateComponent root =
            ComponentTree.Template<DelayedAsynchronousHostTemplate>();
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(DelayedAsynchronousHostTemplate),
                () => new DelayedAsynchronousHostTemplate(
                    showChild,
                    definition)),
            definition.Registration,
        ]);
        ApplicationContext application = new(
            root,
            factory,
            new EmptyServiceProvider());
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        SuspenseBoundaryContext.Current = boundary;
        try
        {
            renderer.Render(root, host.Root, application);
        }
        finally
        {
            SuspenseBoundaryContext.Current = null;
        }

        boundary.Dependencies.ShouldBeEmpty();

        showChild.Value = true;
        _pump.RunUntilIdle();

        boundary.Dependencies.Count.ShouldBe(1);
        renderer.Render(null, host.Root);
    }

    [Fact]
    public async Task ServerPrefetch_AwaitsTrackedLoadBeforeRenderingResolvedRequest()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent(
                typeof(AsynchronousIdentity),
                _ => load.Task);
        ITemplateComponent request = definition.CreateComponent(
            Arguments(("message", "server")));
        ApplicationContext application = Application(
            request,
            definition,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                static () => new ResolvedTemplate()));
        MountedComponent mounted = MountedComponent.Create(
            application,
            request);

        Task prefetch = mounted.InvokeServerPrefetchAsync();
        prefetch.IsCompleted.ShouldBeFalse();

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        await prefetch;

        ITemplateComponent resolved = mounted.Render()
            .ShouldBeAssignableTo<ITemplateComponent>();
        resolved.TemplateType.ShouldBe(typeof(ResolvedTemplate));
        mounted.AbortMount();
    }

    private static ApplicationContext Application(
        IComponent root,
        AsynchronousComponentDefinition definition,
        params ComponentRegistration[] registrations)
    {
        List<ComponentRegistration> all =
        [
            definition.Registration,
        ];
        all.AddRange(registrations);
        return new ApplicationContext(
            root,
            new ComponentFactory(all),
            new EmptyServiceProvider());
    }

    private static ComponentArguments Arguments(
        params (string Name, object? Value)[] values)
    {
        List<KeyValuePair<string, object?>> arguments = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            arguments.Add(
                new KeyValuePair<string, object?>(
                    values[index].Name,
                    values[index].Value));
        }

        return new ComponentArguments(arguments);
    }

    private sealed class AsynchronousIdentity
    {
    }

    private sealed class ResolvedTemplate : IComponentTemplate
    {
        private readonly Action? _setup;

        internal ResolvedTemplate(Action? setup = null)
        {
            _setup = setup;
        }

        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("message"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            _setup?.Invoke();
            return () => ComponentTree.Text(
                context.Arguments.Get<string>("message") ?? "resolved");
        }
    }

    private sealed class ForwardingTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("message"),
        ];

        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent("ready"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(
                () => context.Emit("ready"));
            return () => ComponentTree.Fragment(
            [
                ComponentTree.Text(
                    context.Arguments.Get<string>("message") ?? string.Empty),
                context.Slots["default"](new ComponentArguments())
                    ?? ComponentTree.Comment(),
            ]);
        }
    }

    private sealed class ExposingResolvedTemplate : IComponentTemplate
    {
        private readonly object _exposed;

        internal ExposingResolvedTemplate(object exposed)
        {
            _exposed = exposed;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Expose(_exposed);
            return static () => ComponentTree.Element("div");
        }
    }

    private sealed class DelayedAsynchronousHostTemplate : IComponentTemplate
    {
        private readonly AsynchronousComponentDefinition _definition;
        private readonly Reference<bool> _showChild;

        internal DelayedAsynchronousHostTemplate(
            Reference<bool> showChild,
            AsynchronousComponentDefinition definition)
        {
            _showChild = showChild;
            _definition = definition;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => _showChild.Value
                ? _definition.CreateComponent()
                : ComponentTree.Comment();
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    private sealed class RecordingSuspenseBoundary : ISuspenseBoundary
    {
        internal List<(IComponentContext Component, Task PendingLoad)> Dependencies { get; } = [];

        public void RegisterAsynchronousDependency(
            IComponentContext component,
            Task pendingLoad)
        {
            Dependencies.Add((component, pendingLoad));
        }
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(
            SendOrPostCallback callback,
            object? state)
        {
            callback(state);
        }

        public override void Send(
            SendOrPostCallback callback,
            object? state)
        {
            callback(state);
        }
    }

    private sealed class ManualTimeController : IDisposable
    {
        private readonly List<Entry> _entries = [];
        private readonly Func<int, Action, IDisposable>? _previous;
        private long _now;

        internal ManualTimeController()
        {
            _previous = AsynchronousComponentDelay.Scheduler;
            AsynchronousComponentDelay.Scheduler = Schedule;
        }

        internal void Advance(int milliseconds)
        {
            _now += milliseconds;
            List<Entry> due = _entries.FindAll(
                entry =>
                    !entry.IsCanceled
                    && !entry.HasFired
                    && entry.DueAt <= _now);
            due.Sort(
                static (left, right) => left.DueAt.CompareTo(right.DueAt));
            for (int index = 0; index < due.Count; index++)
            {
                due[index].HasFired = true;
                due[index].Callback();
            }
        }

        public void Dispose()
        {
            AsynchronousComponentDelay.Scheduler = _previous;
        }

        private IDisposable Schedule(
            int milliseconds,
            Action callback)
        {
            Entry entry = new(_now + milliseconds, callback);
            _entries.Add(entry);
            return entry;
        }

        private sealed class Entry : IDisposable
        {
            internal Entry(long dueAt, Action callback)
            {
                DueAt = dueAt;
                Callback = callback;
            }

            internal long DueAt { get; }

            internal Action Callback { get; }

            internal bool IsCanceled { get; private set; }

            internal bool HasFired { get; set; }

            public void Dispose()
            {
                IsCanceled = true;
            }
        }
    }
}
