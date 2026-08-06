using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Tests;

public sealed class ApplicationLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Use_AfterExecutionBegins_Throws()
    {
        InMemoryApplication application = CreateApplication([]);
        Task execution = application.RunAsync().AsTask();

        // [APP-3] Middleware registration freezes synchronously when execution begins.
        Should.Throw<InvalidOperationException>(
            () => application.Use(static (_, next) =>
                next(_)));

        await application.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();
        await execution;
    }

    [Fact]
    public async Task Use_SameMiddlewareRegisteredTwice_ExecutesTwice()
    {
        int entryCount = 0;
        int cleanupCount = 0;
        InMemoryApplication application = CreateApplication([]);
        ApplicationMiddleware middleware = async (execution, next) =>
        {
            entryCount++;
            try
            {
                await next(execution);
            }
            finally
            {
                cleanupCount++;
            }
        };
        application.Use(middleware);
        application.Use(middleware);

        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();
        await execution;

        // [APP-3] Registrations retain multiplicity; there is no plugin-style deduplication.
        entryCount.ShouldBe(2);
        cleanupCount.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_MountedTerminalRemainsPendingAndSecondRunThrows()
    {
        InMemoryApplication application = CreateApplication([]);

        Task execution = application.RunAsync().AsTask();

        // [APP-1]/[APP-4] Run is single-use and spans the entire mounted lifetime.
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = application.RunAsync();
        });

        await application.Mounted.WaitAsync(TestTimeout);
        execution.IsCompleted.ShouldBeFalse();
        application.IsRunning.ShouldBeTrue();
        await application.StopAsync();
        await execution;
        application.IsRunning.ShouldBeFalse();
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task StopAsync_TwoMiddleware_UnmountsThenCleansUpInReverseOrder()
    {
        List<string> order = [];
        InMemoryApplication application = CreateApplication(order);
        application.Use(async (execution, next) =>
        {
            order.Add("first before");
            try
            {
                await next(execution);
            }
            finally
            {
                order.Add("first cleanup");
            }
        });
        application.Use(async (execution, next) =>
        {
            order.Add("second before");
            try
            {
                await next(execution);
            }
            finally
            {
                order.Add("second cleanup");
            }
        });

        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();
        await execution;

        // [APP-4]/[APP-5] The terminal owns the live wait and unmount precedes reverse cleanup.
        order.ShouldBe(
        [
            "first before",
            "second before",
            "initialize",
            "resolve:17",
            "mount:17",
            "unmount",
            "second cleanup",
            "first cleanup",
        ]);
    }

    [Fact]
    public async Task RunAsync_Cancellation_UnmountsAndRunsMiddlewareCleanup()
    {
        List<string> order = [];
        using CancellationTokenSource cancellationSource = new();
        InMemoryApplication application = CreateApplication(order);
        application.Use(async (execution, next) =>
        {
            order.Add("middleware before");
            try
            {
                await next(execution);
            }
            finally
            {
                execution.Stopping.IsCancellationRequested.ShouldBeTrue();
                order.Add("middleware cleanup");
            }
        });

        Task execution = application.RunAsync(cancellationSource.Token).AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        cancellationSource.Cancel();
        await execution;

        // [APP-5] Caller cancellation is graceful application shutdown, not a failed execution.
        order.ShouldBe(
        [
            "middleware before",
            "initialize",
            "resolve:17",
            "mount:17",
            "unmount",
            "middleware cleanup",
        ]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ClaimsThenStopsWithoutMounting()
    {
        List<string> order = [];
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        InMemoryApplication application = CreateApplication(order);
        application.Use(async (executionContext, next) =>
        {
            order.Add("middleware before");
            try
            {
                await next(executionContext);
            }
            finally
            {
                executionContext.Stopping.IsCancellationRequested.ShouldBeTrue();
                order.Add("middleware cleanup");
            }
        });

        ValueTask execution = application.RunAsync(cancellationSource.Token);

        // [APP-1] The execution claim happens before RunAsync returns: even if the asynchronous
        // continuation has already advanced to Stopping, middleware is frozen and another run is
        // rejected synchronously.
        Should.Throw<InvalidOperationException>(() =>
            application.Use(static (context, next) => next(context)));
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = application.RunAsync();
        });
        await execution;

        application.State.ShouldBe(ApplicationState.Stopped);
        application.Mounted.IsCompleted.ShouldBeFalse();
        order.ShouldBe(["middleware before", "middleware cleanup"]);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringInitialization_SkipsResolutionAndMount()
    {
        List<string> order = [];
        using CancellationTokenSource cancellationSource = new();
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryApplication application = CreateApplication(order);
        application.InitializationRelease = initializationRelease;
        application.IgnoreCancellationDuringInitialization = true;
        application.Use(async (executionContext, next) =>
        {
            order.Add("middleware before");
            try
            {
                await next(executionContext);
            }
            finally
            {
                order.Add("middleware cleanup");
            }
        });

        Task execution = application.RunAsync(cancellationSource.Token).AsTask();
        await application.InitializationStarted.WaitAsync(TestTimeout);
        cancellationSource.Cancel();
        initializationRelease.TrySetResult();
        await execution;

        order.ShouldBe(
        [
            "middleware before",
            "initialize",
            "middleware cleanup",
        ]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringResolution_SkipsMount()
    {
        List<string> order = [];
        using CancellationTokenSource cancellationSource = new();
        TaskCompletionSource resolutionRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryApplication application = CreateApplication(order);
        application.ResolutionRelease = resolutionRelease;
        application.IgnoreCancellationDuringResolution = true;

        Task execution = application.RunAsync(cancellationSource.Token).AsTask();
        await application.ResolutionStarted.WaitAsync(TestTimeout);
        cancellationSource.Cancel();
        resolutionRelease.TrySetResult();
        await execution;

        order.ShouldBe(["initialize", "resolve:17"]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task MountAsync_CancellationDuringInitialization_SkipsMount()
    {
        List<string> order = [];
        using CancellationTokenSource cancellationSource = new();
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryApplication application = CreateApplication(order);
        application.InitializationRelease = initializationRelease;
        application.IgnoreCancellationDuringInitialization = true;

        ValueTask<IComponentContext?> execution =
            application.MountAsync(29, cancellationSource.Token);
        await application.InitializationStarted.WaitAsync(TestTimeout);
        cancellationSource.Cancel();
        initializationRelease.TrySetResult();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await execution);
        order.ShouldBe(["initialize"]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task Use_DoesNotChangeFrozenCompositionDependencies()
    {
        TrackingComponentFactory components = new();
        TrackingServiceProvider services = new();
        TrackingStateStoreRegistry state = new();
        TrackingDirectiveResolver directives = new();
        ApplicationContext context = new(
            ComponentTree.Element("main"),
            components,
            services,
            state,
            directives);
        InMemoryApplication application = new(context, []);
        application.Use(async (execution, next) =>
        {
            IApplicationContext current = execution.Application.Context;
            current.Components.ShouldBeSameAs(components);
            current.Services.ShouldBeSameAs(services);
            current.State.ShouldBeSameAs(state);
            current.Directives.ShouldBeSameAs(directives);
            await next(execution);
        });

        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();
        await execution;

        // [APP-2] Use decorates runtime execution and has no composition side effects.
        context.Components.ShouldBeSameAs(components);
        context.Services.ShouldBeSameAs(services);
        context.State.ShouldBeSameAs(state);
        context.Directives.ShouldBeSameAs(directives);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDisposeBorrowedCompositionDependencies()
    {
        TrackingComponentFactory components = new();
        TrackingServiceProvider services = new();
        TrackingStateStoreRegistry state = new();
        TrackingDirectiveResolver directives = new();
        InMemoryApplication application = new(
            new ApplicationContext(
                ComponentTree.Element("main"),
                components,
                services,
                state,
                directives),
            []);

        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        await application.DisposeAsync();
        await execution;

        // [APP-6] The composition root, not Viu, owns every supplied dependency.
        components.IsDisposed.ShouldBeFalse();
        services.IsDisposed.ShouldBeFalse();
        state.IsDisposed.ShouldBeFalse();
        directives.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_MiddlewareFailure_RunsEnteredCleanupAndMarksFailed()
    {
        List<string> order = [];
        InMemoryApplication application = CreateApplication(order);
        application.Use(async (execution, next) =>
        {
            order.Add("outer before");
            try
            {
                await next(execution);
            }
            finally
            {
                order.Add("outer cleanup");
            }
        });
        application.Use(static (_, _) =>
            ValueTask.FromException(
                new InvalidOperationException("middleware failed")));

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await application.RunAsync());

        exception.Message.ShouldBe("middleware failed");
        order.ShouldBe(["outer before", "outer cleanup"]);
        application.State.ShouldBe(ApplicationState.Failed);
        application.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task StopAsync_MiddlewareCleanupFailure_TransitionsStoppingToFailed()
    {
        InMemoryApplication application = CreateApplication([]);
        application.Use(async (execution, next) =>
        {
            await next(execution);
            application.State.ShouldBe(ApplicationState.Stopping);
            throw new InvalidOperationException("cleanup failed");
        });
        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await application.StopAsync());

        exception.Message.ShouldBe("cleanup failed");
        application.State.ShouldBe(ApplicationState.Failed);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await execution);
    }

    [Fact]
    public async Task RunAsync_PartialMountFailure_UnmountsBeforeMiddlewareCleanup()
    {
        List<string> order = [];
        InMemoryApplication application = CreateApplication(order);
        application.ThrowDuringMount = true;
        application.Use(async (execution, next) =>
        {
            order.Add("middleware before");
            try
            {
                await next(execution);
            }
            finally
            {
                order.Add("middleware cleanup");
            }
        });

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await application.RunAsync());

        order.ShouldBe(
        [
            "middleware before",
            "initialize",
            "resolve:17",
            "mount:17",
            "unmount",
            "middleware cleanup",
        ]);
        application.State.ShouldBe(ApplicationState.Failed);
    }

    [Fact]
    public async Task DisposeAsync_DuringStartup_CancelsAndAwaitsMiddlewareCleanup()
    {
        List<string> order = [];
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryApplication application = CreateApplication(order);
        application.InitializationRelease = initializationRelease;
        application.Use(async (execution, next) =>
        {
            order.Add("middleware before");
            try
            {
                await next(execution);
            }
            finally
            {
                order.Add("middleware cleanup");
            }
        });

        Task execution = application.RunAsync().AsTask();
        await application.InitializationStarted.WaitAsync(TestTimeout);
        await application.DisposeAsync();
        await execution;

        order.ShouldBe(
        [
            "middleware before",
            "initialize",
            "middleware cleanup",
        ]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task MountAsync_LowerLevelEntryBypassesMiddlewareAndUsesStopForTeardown()
    {
        int middlewareExecutions = 0;
        List<string> order = [];
        InMemoryApplication application = CreateApplication(order);
        application.Use(async (execution, next) =>
        {
            middlewareExecutions++;
            await next(execution);
        });

        await application.MountAsync(29);

        // [APP-7] Direct embedding mounts bypass the top-level lifetime pipeline.
        middlewareExecutions.ShouldBe(0);
        application.IsRunning.ShouldBeTrue();
        order.ShouldBe(["initialize", "mount:29"]);

        await application.StopAsync();
        order.ShouldBe(["initialize", "mount:29", "unmount"]);
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task StopAsync_CallerCancellationDoesNotCancelApplicationCleanup()
    {
        TaskCompletionSource unmountRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryApplication application = CreateApplication([]);
        application.UnmountRelease = unmountRelease;
        Task execution = application.RunAsync().AsTask();
        await application.Mounted.WaitAsync(TestTimeout);
        using CancellationTokenSource waitCancellationSource = new();

        Task firstWait = application
            .StopAsync(waitCancellationSource.Token)
            .AsTask();
        await application.UnmountStarted.WaitAsync(TestTimeout);
        waitCancellationSource.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await firstWait);

        application.IsRunning.ShouldBeFalse();
        application.State.ShouldBe(ApplicationState.Stopping);
        unmountRelease.SetResult();
        await application.StopAsync();
        await execution;
        application.State.ShouldBe(ApplicationState.Stopped);
    }

    private static InMemoryApplication CreateApplication(List<string> order)
    {
        return new InMemoryApplication(
            new ApplicationContext(
                ComponentTree.Element("main"),
                new ComponentFactory(Array.Empty<ComponentRegistration>()),
                new TrackingServiceProvider()),
            order);
    }

    private sealed class InMemoryApplication : Application<int>
    {
        private readonly List<string> _order;

        internal InMemoryApplication(
            IApplicationContext context,
            List<string> order)
            : base(context)
        {
            _order = order;
        }

        internal Task InitializationStarted => _initializationStarted.Task;

        internal Task Mounted => _mounted.Task;

        internal Task ResolutionStarted => _resolutionStarted.Task;

        internal Task UnmountStarted => _unmountStarted.Task;

        internal TaskCompletionSource? InitializationRelease { get; set; }

        internal TaskCompletionSource? ResolutionRelease { get; set; }

        internal TaskCompletionSource? UnmountRelease { get; set; }

        internal bool IgnoreCancellationDuringInitialization { get; set; }

        internal bool IgnoreCancellationDuringResolution { get; set; }

        internal bool ThrowDuringMount { get; set; }

        internal ApplicationState StateDuringInitialization { get; private set; }

        private readonly TaskCompletionSource _initializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _mounted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resolutionStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _unmountStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask OnInitializeAsync(
            CancellationToken cancellationToken)
        {
            StateDuringInitialization = State;
            _order.Add("initialize");
            _initializationStarted.TrySetResult();
            if (!IgnoreCancellationDuringInitialization)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (InitializationRelease is not null)
            {
                if (IgnoreCancellationDuringInitialization)
                {
                    await InitializationRelease.Task.ConfigureAwait(false);
                }
                else
                {
                    await InitializationRelease.Task
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        protected override async ValueTask<int> ResolveMountTargetAsync(
            CancellationToken cancellationToken)
        {
            if (!IgnoreCancellationDuringResolution)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            _order.Add("resolve:17");
            _resolutionStarted.TrySetResult();
            if (ResolutionRelease is not null)
            {
                if (IgnoreCancellationDuringResolution)
                {
                    await ResolutionRelease.Task.ConfigureAwait(false);
                }
                else
                {
                    await ResolutionRelease.Task
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return 17;
        }

        protected override IComponentContext? MountCore(int container)
        {
            _order.Add($"mount:{container}");
            if (ThrowDuringMount)
            {
                throw new InvalidOperationException("mount failed");
            }

            _mounted.TrySetResult();
            return null;
        }

        protected override void UnmountCore()
        {
            _order.Add("unmount");
            _unmountStarted.TrySetResult();
        }

        protected override async ValueTask UnmountCoreAsync(
            CancellationToken cancellationToken)
        {
            _order.Add("unmount");
            _unmountStarted.TrySetResult();
            if (UnmountRelease is not null)
            {
                await UnmountRelease.Task.ConfigureAwait(false);
            }
        }
    }

    private sealed class TrackingComponentFactory :
        IComponentFactory,
        IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public IComponentTemplate Create(Type componentType)
        {
            throw new InvalidOperationException("No templates are configured for this test.");
        }

        public IComponentTemplate Create(string name)
        {
            throw new InvalidOperationException("No templates are configured for this test.");
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TrackingServiceProvider :
        IServiceProvider,
        IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public object? GetService(Type serviceType)
        {
            return null;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TrackingStateStoreRegistry : IStateStoreRegistry
    {
        public int Count => 0;

        public bool IsDisposed { get; private set; }

        public TStore GetOrCreate<TStore>(
            StateStoreDefinition<TStore> definition,
            IComponentContext? owner = null)
            where TStore : class
        {
            throw new InvalidOperationException("No state stores are configured for this test.");
        }

        public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class
        {
            return false;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TrackingDirectiveResolver :
        IDirectiveResolver,
        IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public IDirective? Resolve(string name)
        {
            return null;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
