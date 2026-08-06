using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>Pins the seven application-lifetime rules against the production Browser host.</summary>
[SupportedOSPlatform("browser")]
public sealed class ApplicationLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Build_ConfigureApplicationFreezesCompositionBeforeRuntimeUse()
    {
        IComponent root = ComponentTree.Element("main");
        TrackingComponentFactory components = new();
        TrackingServiceProvider services = new();
        TrackingStateStoreRegistry state = new();
        TrackingDirectiveResolver directives = new();
        Action<string> warningHandler = static _ => { };
        Action<Exception, IComponentContext?, string> errorHandler =
            static (_, _, _) => { };
        ApplicationOptions? configuredOptions = null;
        BrowserApplicationBuilder builder = new BrowserApplicationBuilder()
            .ConfigureApplication(options =>
            {
                configuredOptions = options;
                options.RootComponent = root;
                options.Components = components;
                options.Services = services;
                options.State = state;
                options.Directives = directives;
                options.WarnHandler = warningHandler;
                options.ErrorHandler = errorHandler;
            });

        BrowserApplication application = builder.Build();
        configuredOptions!.RootComponent = ComponentTree.Comment("replacement");
        configuredOptions.Components = new TrackingComponentFactory();
        configuredOptions.Services = new TrackingServiceProvider();
        configuredOptions.State = null;
        configuredOptions.Directives = null;
        configuredOptions.WarnHandler = null;
        configuredOptions.ErrorHandler = null;

        // [APP-2] ConfigureApplication is the sole builder composition surface and Build snapshots it.
        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.Create("Tracked").ShouldBeOfType<TrackedTemplate>();
        components.CreateCount.ShouldBe(1);
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.State.ShouldBeSameAs(state);
        application.Context.Directives.ShouldBeSameAs(directives);
        application.Context.WarnHandler.ShouldBeSameAs(warningHandler);
        application.Context.ErrorHandler.ShouldBeSameAs(errorHandler);
    }

    [Fact]
    public async Task Use_DoesNotChangeFrozenCompositionDependencies()
    {
        List<string> order = [];
        TrackingComponentFactory components = new();
        TrackingServiceProvider services = new();
        TrackingStateStoreRegistry state = new();
        TrackingDirectiveResolver directives = new();
        (BrowserApplication application, LifetimeHost host) = CreateApplication(
            order,
            components,
            services,
            state,
            directives);

        application.Use(static (context, next) => next(context));

        // [APP-2] Runtime middleware cannot replace composition frozen into the context.
        application.Context.Components.ShouldBeSameAs(components);
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.State.ShouldBeSameAs(state);
        application.Context.Directives.ShouldBeSameAs(directives);

        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();
    }

    [Fact]
    public async Task Use_AfterStartAsyncBegins_Throws()
    {
        List<string> order = [];
        TaskCompletionSource initializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        (BrowserApplication application, _) = CreateApplication(
            order,
            initialize: async cancellationToken =>
            {
                initializationStarted.TrySetResult();
                await initializationRelease.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            });

        ValueTask starting = application.StartAsync();

        // [APP-3] The synchronous execution claim freezes middleware before startup yields.
        Should.Throw<InvalidOperationException>(() =>
            application.Use(static (context, next) => next(context)));
        await initializationStarted.Task.WaitAsync(TestTimeout);
        initializationRelease.TrySetResult();
        await starting;
        await application.StopAsync();
    }

    [Fact]
    public async Task Use_SameMiddlewareRegisteredTwice_ExecutesTwice()
    {
        List<string> order = [];
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order);
        int entryCount = 0;
        int cleanupCount = 0;
        ApplicationMiddleware middleware = async (context, next) =>
        {
            entryCount++;
            try
            {
                await next(context);
            }
            finally
            {
                cleanupCount++;
            }
        };

        application.Use(middleware).Use(middleware);
        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();

        // [APP-3] Registrations retain multiplicity rather than using plugin-style deduplication.
        entryCount.ShouldBe(2);
        cleanupCount.ShouldBe(2);
    }

    [Fact]
    public async Task StartAsync_WaitsForRunningSignalAndSecondCallThrows()
    {
        List<string> order = [];
        TaskCompletionSource initializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource initializationRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        (BrowserApplication application, LifetimeHost host) = CreateApplication(
            order,
            initialize: async cancellationToken =>
            {
                initializationStarted.TrySetResult();
                await initializationRelease.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            });

        ValueTask starting = application.StartAsync();

        // [APP-1] Start claims synchronously, but completes only after the terminal mounts.
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = application.StartAsync();
        });
        await initializationStarted.Task.WaitAsync(TestTimeout);
        starting.IsCompleted.ShouldBeFalse();
        application.Context.IsRunning.ShouldBeFalse();

        initializationRelease.TrySetResult();
        await starting;
        await host.Mounted.WaitAsync(TestTimeout);
        application.Context.IsRunning.ShouldBeTrue();
        await application.StopAsync();
    }

    [Fact]
    public async Task StopAsync_BeforeStartDoesNotConsumeTheSingleUseLifetime()
    {
        List<string> order = [];
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order);

        await application.StopAsync();
        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();

        order.ShouldContain("mount");
        order.ShouldContain("unmount");
    }

    [Fact]
    public async Task RunAsync_MountedTerminalRemainsPendingUntilShutdown()
    {
        List<string> order = [];
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order);
        application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            finally
            {
                order.Add("middleware cleanup");
            }
        });

        Task execution = application.RunAsync().AsTask();
        await host.Mounted.WaitAsync(TestTimeout);
        await WaitUntilAsync(() => application.Context.IsRunning);

        // [APP-4] The extension must not mask an early terminal return.
        execution.IsCompleted.ShouldBeFalse();
        order.ShouldNotContain("unmount");
        order.ShouldNotContain("middleware cleanup");

        await application.StopAsync();
        await execution;
        order.ShouldContain("unmount");
        order.ShouldContain("middleware cleanup");
    }

    [Fact]
    public async Task StopAsync_TwoMiddleware_UnmountsThenCleansUpInReverseOrder()
    {
        List<string> order = [];
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order);
        application.Use(async (context, next) =>
        {
            order.Add("first before");
            try
            {
                await next(context);
            }
            finally
            {
                order.Add("first cleanup");
            }
        });
        application.Use(async (context, next) =>
        {
            order.Add("second before");
            try
            {
                await next(context);
            }
            finally
            {
                order.Add("second cleanup");
            }
        });

        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        await application.StopAsync();

        // [APP-4], [APP-5] Unmount precedes reverse-order middleware cleanup.
        order.ShouldBe(
        [
            "first before",
            "second before",
            "mount",
            "unmount",
            "second cleanup",
            "first cleanup",
        ]);
        application.Context.IsRunning.ShouldBeFalse();
        application.Context.Stopping.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ClaimsThenStopsWithoutMounting()
    {
        List<string> order = [];
        (BrowserApplication application, _) = CreateApplication(order);
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();

        await application.RunAsync(cancellationSource.Token);

        // [APP-1], [APP-5] Cancellation is observed after the single-use claim.
        order.ShouldNotContain("mount");
        application.Context.IsRunning.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = application.StartAsync();
        });
    }

    [Fact]
    public async Task StopAsync_PostStartupMiddlewareFailureReportsAndSurfaces()
    {
        List<string> order = [];
        InvalidOperationException failure = new("cleanup failed");
        List<Exception> reported = [];
        ApplicationOptions diagnostics = new()
        {
            ErrorHandler = (exception, _, _) => reported.Add(exception),
        };
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order, diagnostics: diagnostics);
        application.Use(async (context, next) =>
        {
            await next(context);
            throw failure;
        });

        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        InvalidOperationException surfaced =
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await application.StopAsync());

        // [APP-5] A background pipeline failure is reported once and retained by StopAsync.
        surfaced.ShouldBeSameAs(failure);
        reported.ShouldBe([failure]);
        application.HasFailed.ShouldBeTrue();
        application.Context.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_PostStartupMiddlewareFailureReportsAndSurfaces()
    {
        List<string> order = [];
        InvalidOperationException failure = new("cleanup failed");
        List<Exception> reported = [];
        ApplicationOptions diagnostics = new()
        {
            ErrorHandler = (exception, _, _) => reported.Add(exception),
        };
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order, diagnostics: diagnostics);
        application.Use(async (context, next) =>
        {
            await next(context);
            throw failure;
        });
        using CancellationTokenSource cancellationSource = new();

        Task execution = application.RunAsync(cancellationSource.Token).AsTask();
        await host.Mounted.WaitAsync(TestTimeout);
        cancellationSource.Cancel();
        InvalidOperationException surfaced =
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await execution);

        // [APP-5] The full-lifetime extension surfaces the retained background pipeline fault.
        surfaced.ShouldBeSameAs(failure);
        reported.ShouldBe([failure]);
        application.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task MountAsync_LowerLevelEntryBypassesMiddlewareAndUsesStopForTeardown()
    {
        List<string> order = [];
        (BrowserApplication application, LifetimeHost host) =
            CreateApplication(order);
        int middlewareCount = 0;
        application.Use((context, next) =>
        {
            middlewareCount++;
            return next(context);
        });

        await application.MountAsync(LifetimeHost.Container);
        await host.Mounted.WaitAsync(TestTimeout);

        // [APP-7] Browser embedding mount operations bypass top-level middleware.
        middlewareCount.ShouldBe(0);
        application.Context.IsRunning.ShouldBeTrue();
        await application.StopAsync();
        middlewareCount.ShouldBe(0);
        order.ShouldContain("unmount");
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDisposeBorrowedCompositionDependencies()
    {
        List<string> order = [];
        TrackingComponentFactory components = new();
        TrackingServiceProvider services = new();
        TrackingStateStoreRegistry state = new();
        TrackingDirectiveResolver directives = new();
        (BrowserApplication application, LifetimeHost host) = CreateApplication(
            order,
            components,
            services,
            state,
            directives);

        await application.StartAsync();
        await host.Mounted.WaitAsync(TestTimeout);
        await application.DisposeAsync();

        // [APP-6] The application borrows every composition dependency.
        components.IsDisposed.ShouldBeFalse();
        services.IsDisposed.ShouldBeFalse();
        state.IsDisposed.ShouldBeFalse();
        directives.IsDisposed.ShouldBeFalse();
    }

    private static (
        BrowserApplication Application,
        LifetimeHost Host) CreateApplication(
        List<string> order,
        IComponentFactory? components = null,
        IServiceProvider? services = null,
        IStateStoreRegistry? state = null,
        IDirectiveResolver? directives = null,
        ApplicationOptions? diagnostics = null,
        Func<CancellationToken, Task>? initialize = null)
    {
        LifetimeHost host = new(order);
        ApplicationContext context = new(
            ComponentTree.Element("main"),
            components ?? new TrackingComponentFactory(),
            services ?? new TrackingServiceProvider(),
            state,
            directives,
            diagnostics);
        BrowserApplication application = new(
            RendererFactory.CreateRenderer(host.CreateOptions()),
            context,
            initialize: initialize ?? (static _ => Task.CompletedTask),
            clearContainer: static _ => { },
            resolveContainer: static _ => LifetimeHost.Container);
        return (application, host);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeoutSource = new(TestTimeout);
        while (!condition())
        {
            await Task.Delay(1, timeoutSource.Token);
        }
    }

    private sealed class LifetimeHost(List<string> order)
    {
        internal const int Container = 29;

        private int _nextNode;

        internal Task Mounted => _mounted.Task;

        private readonly TaskCompletionSource _mounted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal RendererOptions<int> CreateOptions()
        {
            return new RendererOptions<int>
            {
                Insert = (_, parent, _) =>
                {
                    if (parent == Container)
                    {
                        order.Add("mount");
                        _mounted.TrySetResult();
                    }
                },
                Remove = _ => order.Add("unmount"),
                CreateElement = static (_, _) => 1,
                CreateText = _ => ++_nextNode,
                CreateComment = _ => ++_nextNode,
                SetText = static (_, _) => { },
                ParentNode = static _ => default,
                NextSibling = static _ => default,
                PatchAttribute = static (_, _, _, _, _, _) => { },
            };
        }
    }

    private sealed class TrackingComponentFactory :
        IComponentFactory,
        IDisposable
    {
        internal int CreateCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public IComponentTemplate Create(Type componentType)
        {
            ArgumentNullException.ThrowIfNull(componentType);
            CreateCount++;
            return new TrackedTemplate();
        }

        public IComponentTemplate Create(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            CreateCount++;
            return new TrackedTemplate();
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
            ArgumentNullException.ThrowIfNull(serviceType);
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
            ArgumentNullException.ThrowIfNull(definition);
            throw new InvalidOperationException("No state stores are configured for this test.");
        }

        public bool Remove<TStore>(StateStoreDefinition<TStore> definition)
            where TStore : class
        {
            ArgumentNullException.ThrowIfNull(definition);
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
            ArgumentException.ThrowIfNullOrEmpty(name);
            return null;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TrackedTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static () => ComponentTree.Comment("tracked");
        }
    }
}
