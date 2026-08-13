using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser's public D5a application seam and hydration bootstrap [APP-1..7], [HYD-1..4].
public sealed class BrowserApplicationTests
{
    [Fact]
    public async Task StartAndStopAsync_MiddlewareSurroundsTheMountedLifetime()
    {
        var host = new BrowserRendererHost((_, _) => []);
        host.ObserveForeignHandle(100);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        ApplicationContext context = CreateContext(
            new ElementNode(
                new QualifiedName("main"),
                children: [new TextNode("ready")]));
        int clearCount = 0;
        var application = BrowserApplication.CreateEmbedded(
            renderer,
            context,
            initialize: _ => Task.CompletedTask,
            clearContainer: _ => clearCount++,
            resolveContainer: _ => 100);
        List<string> order = [];
        application.Use(
            async (_, next) =>
            {
                order.Add("before");
                try
                {
                    await next(context);
                }
                finally
                {
                    order.Add("after");
                }
            });

        await application.StartAsync();

        context.IsRunning.ShouldBeTrue();
        order.ShouldBe(["before"]);
        clearCount.ShouldBe(1);
        host.InteropCallCount.ShouldBe(1);

        await application.StopAsync();

        context.IsRunning.ShouldBeFalse();
        context.Stopping.IsCancellationRequested.ShouldBeTrue();
        order.ShouldBe(["before", "after"]);
        host.InteropCallCount.ShouldBe(2);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task MountAsync_Hydration_AdoptsTheBrowserSnapshotWithoutClearing()
    {
        const string snapshot =
            "2 100 0 101 0 0 4:MAIN 0 101 100 0 0 0 3:DIV 0 ";
        int snapshotCount = 0;
        int clearCount = 0;
        var host = new BrowserRendererHost(
            (_, _) => [],
            snapshotHydration: _ =>
            {
                snapshotCount++;
                return snapshot;
            });
        host.ObserveForeignHandle(100);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        ApplicationContext context = CreateContext(
            new ElementNode(new QualifiedName("div")));
        var application = BrowserApplication.CreateEmbedded(
            renderer,
            context,
            hydrate: true,
            initialize: _ => Task.CompletedTask,
            clearContainer: _ => clearCount++);

        ComponentContext? rootContext = await application.MountAsync(100);

        rootContext.ShouldBeNull();
        context.IsRunning.ShouldBeTrue();
        snapshotCount.ShouldBe(1);
        clearCount.ShouldBe(0);
        host.InteropCallCount.ShouldBe(0);

        await application.StopAsync();

        host.InteropCallCount.ShouldBe(1);
        await application.DisposeAsync();
    }

    [Fact]
    public async Task ScriptHotReload_WhileMounted_RemountsPostFlushWithoutRequestingDocumentReload()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(scheduledFlushes.Enqueue);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(300);
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            HotReloadComponentSource source = new();
            ComponentNode root = new(
                ComponentReference.ForType(typeof(HotReloadComponent)));
            ApplicationContext context = CreateHotReloadContext(root, source);
            var application = BrowserApplication.CreateEmbedded(
                renderer,
                context,
                initialize: _ => Task.CompletedTask,
                clearContainer: _ => { });
            ComponentHotReload.Register(
                typeof(HotReloadComponent),
                "browser-application-hot-reload",
                typeof(TemplateMarker),
                typeof(ScriptMarker),
                typeof(StyleMarker));
            await application.MountAsync(300);
            RunScheduledFlushes(scheduledFlushes);
            HotReloadComponent previous = source.Instances[0];

            // A BrowserDomBridge document-reload request throws in this DOM-free test process, so
            // the no-throw assertion pins that accepted script deltas remain in-process.
            Should.NotThrow(
                () => ComponentHotReload.ApplyUpdates([typeof(ScriptMarker)]));

            source.Instances.Count.ShouldBe(1);
            previous.IsDisposed.ShouldBeFalse();
            RunScheduledFlushes(scheduledFlushes);
            source.Instances.Count.ShouldBe(2);
            previous.IsDisposed.ShouldBeTrue();

            await application.StopAsync();
            RunScheduledFlushes(scheduledFlushes);
            await application.DisposeAsync();
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public async Task MountAsync_ConcurrentBrowserHost_RejectsWithoutDisturbingActiveOwner()
    {
        var firstHost = new BrowserRendererHost((_, _) => []);
        var secondHost = new BrowserRendererHost((_, _) => []);
        firstHost.ObserveForeignHandle(400);
        secondHost.ObserveForeignHandle(500);
        var firstApplication = BrowserApplication.CreateEmbedded(
            firstHost,
            CreateContext(new TextNode("first")),
            initialize: _ => Task.CompletedTask);
        var rejectedApplication = BrowserApplication.CreateEmbedded(
            secondHost,
            CreateContext(new TextNode("rejected")),
            initialize: _ => Task.CompletedTask);
        await firstApplication.MountAsync(400);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await rejectedApplication.MountAsync(500));

        firstHost.InteropCallCount.ShouldBe(1);
        secondHost.InteropCallCount.ShouldBe(0);
        await firstApplication.StopAsync();
        firstHost.InteropCallCount.ShouldBe(2);
        await firstApplication.DisposeAsync();
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await rejectedApplication.DisposeAsync());

        var recoveredApplication = BrowserApplication.CreateEmbedded(
            secondHost,
            CreateContext(new TextNode("recovered")),
            initialize: _ => Task.CompletedTask);
        await recoveredApplication.MountAsync(500);
        secondHost.InteropCallCount.ShouldBe(1);
        await recoveredApplication.StopAsync();
        secondHost.InteropCallCount.ShouldBe(2);
        await recoveredApplication.DisposeAsync();
    }

    private static ApplicationContext CreateContext(VirtualNode root)
    {
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
            });
    }

    private static ApplicationContext CreateHotReloadContext(
        ComponentNode root,
        HotReloadComponentSource source)
    {
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                root.Component,
                new ComponentContract(),
                _ => source.Create()));
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
    }

    private static void RunScheduledFlushes(Queue<Action> scheduledFlushes)
    {
        while (scheduledFlushes.Count > 0)
        {
            scheduledFlushes.Dequeue()();
        }
    }

    private sealed class HotReloadComponentSource
    {
        internal List<HotReloadComponent> Instances { get; } = [];

        internal HotReloadComponent Create()
        {
            var component = new HotReloadComponent();
            Instances.Add(component);
            return component;
        }
    }

    private sealed class HotReloadComponent : IComponent, IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode("mounted");

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TemplateMarker;

    private sealed class ScriptMarker;

    private sealed class StyleMarker;

}
