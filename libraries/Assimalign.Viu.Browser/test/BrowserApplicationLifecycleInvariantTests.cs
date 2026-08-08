using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser's public embedding seam against the application-lifetime contracts [APP-1..7].
public sealed class BrowserApplicationLifecycleInvariantTests
{
    private const int ContainerHandle = 700;

    [Fact]
    public async Task Middleware_NestedAndDuplicateRegistrations_ExecuteInRegistrationAndReverseCleanupOrder()
    {
        (BrowserApplication application, _, ApplicationContext context) =
            CreateApplication();
        List<string> order = [];
        int duplicateInvocationCount = 0;

        ApplicationMiddleware Track(string name) =>
            async (currentContext, next) =>
            {
                order.Add($"{name}-before");
                try
                {
                    await next(currentContext);
                }
                finally
                {
                    order.Add($"{name}-after");
                }
            };
        ApplicationMiddleware duplicate =
            async (currentContext, next) =>
            {
                int invocation = ++duplicateInvocationCount;
                order.Add($"duplicate-{invocation}-before");
                try
                {
                    await next(currentContext);
                }
                finally
                {
                    order.Add($"duplicate-{invocation}-after");
                }
            };

        application.Use(Track("outer"));
        application.Use(duplicate);
        application.Use(duplicate);
        application.Use(Track("inner"));

        await application.StartAsync();

        context.IsRunning.ShouldBeTrue();
        duplicateInvocationCount.ShouldBe(2);
        order.ShouldBe(
        [
            "outer-before",
            "duplicate-1-before",
            "duplicate-2-before",
            "inner-before",
        ]);

        await application.StopAsync();

        order.ShouldBe(
        [
            "outer-before",
            "duplicate-1-before",
            "duplicate-2-before",
            "inner-before",
            "inner-after",
            "duplicate-2-after",
            "duplicate-1-after",
            "outer-after",
        ]);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_AfterExecutionClaim_RejectsMiddlewareAndASecondStart()
    {
        (BrowserApplication application, _, _) = CreateApplication();

        ValueTask startup = application.StartAsync();
        Action useAfterStart = () =>
        {
            application.Use(static (context, next) => next(context));
        };
        Action secondStart = () =>
        {
            _ = application.StartAsync();
        };

        useAfterStart.ShouldThrow<InvalidOperationException>();
        secondStart.ShouldThrow<InvalidOperationException>();

        await startup;
        await application.StopAsync();
        await application.DisposeAsync();
    }

    [Fact]
    public async Task MountAsync_RepeatedMountRejectsAndStopUnmountsTheDirectLifetime()
    {
        (BrowserApplication application, BrowserRendererHost host, ApplicationContext context) =
            CreateApplication();

        _ = await application.MountAsync(ContainerHandle);
        Action repeatedMount = () =>
        {
            _ = application.Mount(ContainerHandle);
        };

        context.IsRunning.ShouldBeTrue();
        host.InteropCallCount.ShouldBe(1);
        repeatedMount.ShouldThrow<InvalidOperationException>();

        await application.StopAsync();

        context.IsRunning.ShouldBeFalse();
        context.Stopping.IsCancellationRequested.ShouldBeTrue();
        host.InteropCallCount.ShouldBe(2);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_CancelledCallerWait_DoesNotCancelSharedMiddlewareCleanup()
    {
        (BrowserApplication application, _, ApplicationContext context) =
            CreateApplication();
        var cleanupStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleanupCompleted = false;
        application.Use(
            async (currentContext, next) =>
            {
                try
                {
                    await next(currentContext);
                }
                finally
                {
                    cleanupStarted.TrySetResult(null);
                    await releaseCleanup.Task;
                    cleanupCompleted = true;
                }
            });
        await application.StartAsync();
        using var callerCancellation = new CancellationTokenSource();

        Task callerWait = application.StopAsync(callerCancellation.Token).AsTask();
        await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callerCancellation.Cancel();

        try
        {
            await Should.ThrowAsync<OperationCanceledException>(
                async () => await callerWait);
            cleanupCompleted.ShouldBeFalse();
            context.Stopping.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            releaseCleanup.TrySetResult(null);
        }

        await application.StopAsync();

        cleanupCompleted.ShouldBeTrue();
        context.IsRunning.ShouldBeFalse();
        await application.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_BorrowedServiceProvider_DoesNotDisposeCompositionDependency()
    {
        var services = new TrackingServiceProvider();
        (BrowserApplication application, _, _) = CreateApplication(services);

        await application.StartAsync();
        await application.StopAsync();
        await application.DisposeAsync();

        services.DisposeCount.ShouldBe(0);
        services.DisposeAsyncCount.ShouldBe(0);
    }

    private static (
        BrowserApplication Application,
        BrowserRendererHost Host,
        ApplicationContext Context) CreateApplication(
            IServiceProvider? services = null)
    {
        var host = new BrowserRendererHost((_, _) => []);
        host.ObserveForeignHandle(ContainerHandle);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        var context = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = new ElementNode(
                    new QualifiedName("main"),
                    children: [new TextNode("ready")]),
                Services = services,
            });
        BrowserApplication application = BrowserApplication.CreateEmbedded(
            renderer,
            context,
            initialize: static _ => Task.CompletedTask,
            clearContainer: static _ => { },
            resolveContainer: static _ => ContainerHandle);
        return (application, host, context);
    }

    private sealed class TrackingServiceProvider :
        IServiceProvider,
        IDisposable,
        IAsyncDisposable
    {
        internal int DisposeCount { get; private set; }

        internal int DisposeAsyncCount { get; private set; }

        public object? GetService(Type serviceType) => null;

        public void Dispose() => DisposeCount++;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }
}
