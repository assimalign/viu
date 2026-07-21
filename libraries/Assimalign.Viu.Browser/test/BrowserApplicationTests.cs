using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Tests;

// Pins the [V01.01.04.04.01] / [V01.01.03.23] application API on BrowserApplication against Vue's app
// object (https://vuejs.org/api/application.html): chainable Component/Directive/Provide/Use/Config
// inherited from the Application<int> base, plus the browser mount path that initializes the bridge
// exactly once inside MountAsync (the reshape removed the external BrowserRuntime.InitializeAsync()
// pre-call). Registration and the DOM-free mount checks run over an inert int renderer with the
// bridge/clear seams substituted; real interop is exercised through the demo app. The platform
// annotation mirrors the type under test.
[SupportedOSPlatform("browser")]
public sealed class BrowserApplicationTests
{
    // An inert int-handle renderer: registration never invokes node operations, and a DOM-free mount
    // drives these no-op ops instead of real interop.
    private static Renderer<int> InertRenderer() => RendererFactory.CreateRenderer(new RendererOptions<int>
    {
        Insert = static (_, _, _) => { },
        Remove = static _ => { },
        CreateElement = static (_, _) => 1,
        CreateText = static _ => 1,
        CreateComment = static _ => 1,
        SetText = static (_, _) => { },
        SetElementText = static (_, _) => { },
        ParentNode = static _ => 0,
        NextSibling = static _ => 0,
        PatchProperty = static (_, _, _, _, _, _) => { },
    });

    private static BrowserApplication CreateApp(
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null,
        RenderComponent? root = null)
        => new(
            InertRenderer(),
            root ?? new RenderComponent(static () => VirtualNodeFactory.Element("div")),
            rootProperties: null,
            bufferedOperations: null,
            hydrate: false,
            initialize: initialize,
            clearContainer: clearContainer);

    [Fact]
    public void Component_RegistersAndResolves()
    {
        var app = CreateApp();
        var child = new RenderComponent(static () => VirtualNodeFactory.Element("span"));

        var returned = app.Component("MyChild", child);

        returned.ShouldBeSameAs(app); // chainable, returns the same application
        app.Component("MyChild").ShouldBeSameAs(child);
        app.Component("Unknown").ShouldBeNull();
    }

    [Fact]
    public void Directive_RegistersAndResolves()
    {
        var app = CreateApp();
        var focus = new Directive { Mounted = static (_, _, _, _) => { } };

        var returned = app.Directive("focus", focus);

        returned.ShouldBeSameAs(app);
        app.Directive("focus").ShouldBeSameAs(focus);
        app.Directive("missing").ShouldBeNull();
    }

    [Fact]
    public void Provide_TypedAndString_LandInTheSharedContext()
    {
        var app = CreateApp();
        var key = new InjectionKey<string>("theme");
        var warnings = new List<string>();
        var previousSink = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = warnings.Add;
        try
        {
            app.Provide(key, "dark").ShouldBeSameAs(app);
            app.Provide("locale", "en-US").ShouldBeSameAs(app);

            // Re-providing the same key warns — proof the provide landed in the shared ApplicationContext
            // (upstream: "App already provides property with key").
            app.Provide(key, "light");
            warnings.ShouldContain(message => message.Contains("already provides"));
        }
        finally
        {
            RuntimeWarnings.Sink = previousSink;
        }
    }

    [Fact]
    public async Task Use_InstallsThePluginOnce_AgainstThisApplication()
    {
        Scheduler.Reset();
        var app = CreateApp(initialize: static _ => Task.CompletedTask, clearContainer: static _ => { });
        var plugin = new CountingPlugin();
        try
        {
            app.Use(plugin).ShouldBeSameAs(app);
            using (CaptureWarnings(out var messages))
            {
                app.Use(plugin).ShouldBeSameAs(app); // repeat Use of the same instance is deduplicated
                messages.ShouldContain(message => message.Contains("already been applied"));
            }

            // Recorded, not yet installed: the plugin installs later, inside the mount path ([V01.01.03.27]).
            plugin.InstallCount.ShouldBe(0);

            await app.MountAsync(container: 5);

            plugin.InstallCount.ShouldBe(1); // installed exactly once, inside MountAsync
            plugin.SeenApplication.ShouldBeSameAs(app); // the plugin receives the app itself (the IApplication)
        }
        finally
        {
            app.Unmount();
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Config_RetainsItsHandlers()
    {
        var app = CreateApp();

        Action<string> warnHandler = static _ => { };
        app.Context.WarnHandler = warnHandler;
        app.Context.WarnHandler.ShouldBeSameAs(warnHandler);
    }

    [Fact]
    public void RegistrationSurface_ChainsFluently()
    {
        var app = CreateApp();
        var child = new RenderComponent(static () => VirtualNodeFactory.Element("span"));
        var focus = new Directive { Mounted = static (_, _, _, _) => { } };

        var returned = app
            .Component("Child", child)
            .Directive("focus", focus)
            .Provide("locale", "en-US")
            .Use(new CountingPlugin());

        returned.ShouldBeSameAs(app);
    }

    [Fact]
    public async Task MountAsync_InitializesTheBridgeExactlyOnce_InsideTheMountPath()
    {
        Scheduler.Reset();
        var initializations = 0;
        var app = CreateApp(
            initialize: _ => { initializations++; return Task.CompletedTask; },
            clearContainer: static _ => { });
        try
        {
            var instance = await app.MountAsync(container: 7);

            initializations.ShouldBe(1); // the bridge initialized exactly once, inside MountAsync
            app.IsMounted.ShouldBeTrue();
            instance.ShouldNotBeNull();

            // A second mount warns and no-ops; initialization does not run again (double-init guarded).
            using var warnings = CaptureWarnings(out var messages);
            await app.MountAsync(container: 7);
            initializations.ShouldBe(1);
            messages.ShouldContain(message => message.Contains("already been mounted"));
        }
        finally
        {
            app.Unmount();
            Scheduler.Reset();
        }
    }

    [Fact]
    public async Task MountAsync_RunsInitialization_BeforeRendering()
    {
        Scheduler.Reset();
        var order = new List<string>();
        var root = new RenderComponent((_, _) =>
        {
            order.Add("setup"); // component setup runs during render, after initialization
            return static () => VirtualNodeFactory.Element("div");
        });
        var app = CreateApp(
            initialize: _ => { order.Add("init"); return Task.CompletedTask; },
            clearContainer: _ => order.Add("clear"),
            root: root);
        try
        {
            await app.MountAsync(container: 3);

            // Documented mount order: OnInitializeAsync -> clear container -> render (setup).
            order.ShouldBe(["init", "clear", "setup"]);
        }
        finally
        {
            app.Unmount();
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Mount_Synchronously_WithoutInitialization_Throws()
    {
        var app = CreateApp(clearContainer: static _ => { });

        // The synchronous advanced path requires the bridge already initialized; without it (and with no
        // prior MountAsync) it throws pointing at MountAsync rather than failing cryptically in interop.
        var thrown = Record.Exception(() => app.Mount(container: 1));

        thrown.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldContain("MountAsync");
        app.IsMounted.ShouldBeFalse();
    }

    private static IDisposable CaptureWarnings(out List<string> messages)
    {
        var captured = new List<string>();
        messages = captured;
        var previous = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = captured.Add;
        return new WarningSinkScope(previous);
    }

    private sealed class WarningSinkScope(Action<string> previous) : IDisposable
    {
        public void Dispose() => RuntimeWarnings.Sink = previous;
    }

    private sealed class CountingPlugin : IApplicationPlugin
    {
        public int InstallCount { get; private set; }

        public IApplication? SeenApplication { get; private set; }

        public ValueTask InstallAsync(IApplication application)
        {
            InstallCount++;
            SeenApplication = application;
            return ValueTask.CompletedTask;
        }
    }
}
