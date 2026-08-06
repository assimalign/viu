using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Router.Browser.Tests;

// Pins the Router<->DOM click bridge ([V01.01.08.03.01], issue #191): the browser adapter's
// BrowserEvent is mapped onto RouterLinkClickEvent (mouse button, the four system modifiers, and the
// arrival-time defaultPrevented state), and the guard's preventDefault decision is propagated back
// onto the live event's response flags. DOM-free through the in-memory Testing renderer;
// real-browser behavior is the e2e harness ([V01.01.11.03]).
public class RouterLinkDomBridgeTests
{
    // --- mapping: BrowserEvent -> RouterLinkClickEvent -------------------------------------------

    [Theory]
    [InlineData(0)] // primary/left
    [InlineData(1)] // middle
    [InlineData(2)] // secondary/right
    public void Invoke_MapsMouseButton(int button)
    {
        var click = Bridge(Click(button: button));
        click.Button.ShouldBe(button);
    }

    [Fact]
    public void Invoke_MapsEachSystemModifier()
    {
        Bridge(Click(modifiers: BrowserEventModifiers.Control)).ControlKey.ShouldBeTrue();
        Bridge(Click(modifiers: BrowserEventModifiers.Shift)).ShiftKey.ShouldBeTrue();
        Bridge(Click(modifiers: BrowserEventModifiers.Alt)).AltKey.ShouldBeTrue();
        Bridge(Click(modifiers: BrowserEventModifiers.Meta)).MetaKey.ShouldBeTrue();
        Bridge(Click()).HasSystemModifier.ShouldBeFalse();
    }

    [Fact]
    public void Invoke_SeedsAlreadyPreventedState_SoTheGuardBails()
    {
        // An event that arrived prevented -> the RouterLinkClickEvent reads DefaultPrevented, so the
        // guard falls through and the bridge never re-signals.
        Bridge(Click(defaultPrevented: true)).DefaultPrevented.ShouldBeTrue();
    }

    // --- propagation: guard decision -> live event -----------------------------------------------

    [Fact]
    public void Invoke_WhenGuardPreventsDefault_PreventsTheLiveEvent()
    {
        var browserEvent = Click();
        // A handler that intercepts, as RouterLink does for an unmodified primary-button click.
        RouterLinkDomBridge.Invoke(value => ((RouterLinkClickEvent)value!).PreventDefault(), browserEvent);
        browserEvent.ToResponseFlags().ShouldBe(2); // preventDefault re-crosses the boundary to JS
    }

    [Fact]
    public void Invoke_WhenGuardDoesNotIntercept_LeavesTheLiveEventAlone()
    {
        var browserEvent = Click(button: 1);
        RouterLinkDomBridge.Invoke(_ => { }, browserEvent); // fall-through handler
        browserEvent.ToResponseFlags().ShouldBe(0);
    }

    [Fact]
    public void Invoke_WhenEventArrivedPrevented_DoesNotResignalPreventDefault()
    {
        // The browser already applied the arrival prevent, so a bailing guard re-crosses nothing.
        var browserEvent = Click(defaultPrevented: true);
        RouterLinkDomBridge.Invoke(_ => { }, browserEvent);
        browserEvent.ToResponseFlags().ShouldBe(0);
    }

    [Fact]
    public void Invoke_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => RouterLinkDomBridge.Invoke(null!, Click()));
        Should.Throw<ArgumentNullException>(() => RouterLinkDomBridge.Invoke(_ => { }, null!));
    }

    // --- install / uninstall ---------------------------------------------------------------------

    [Fact]
    public void InstallAndUninstall_SetAndClearTheDomEventBridge()
    {
        var previous = BrowserObjectEvents.Invoker;
        try
        {
            BrowserObjectEvents.Invoker = null;
            RouterLinkDomBridge.Install();
            BrowserObjectEvents.Invoker.ShouldNotBeNull();

            RouterLinkDomBridge.Uninstall();
            BrowserObjectEvents.Invoker.ShouldBeNull();
        }
        finally
        {
            BrowserObjectEvents.Invoker = previous;
        }
    }

    [Fact]
    public void Uninstall_LeavesAForeignInvokerUntouched()
    {
        var previous = BrowserObjectEvents.Invoker;
        try
        {
            BrowserObjectEventInvoker foreign = (_, _) => { };
            BrowserObjectEvents.Invoker = foreign;
            RouterLinkDomBridge.Uninstall(); // not our invoker -> no-op
            BrowserObjectEvents.Invoker.ShouldBeSameAs(foreign);
        }
        finally
        {
            BrowserObjectEvents.Invoker = previous;
        }
    }

    [Fact]
    public async Task UseRouter_InstallsBeforeReadinessAndUninstallsAfterApplicationStops()
    {
        BrowserObjectEventInvoker? previous = BrowserObjectEvents.Invoker;
        List<string> order = [];
        int navigationCount = 0;
        try
        {
            BrowserObjectEvents.Invoker = null;
            using Router router = new(
                RouterHistory.CreateMemory(),
                [new RouteRecord("/"), new RouteRecord("/next")]);
            router.BeforeEach((_, _, _) =>
            {
                navigationCount++;
                if (navigationCount == 1)
                {
                    BrowserObjectEvents.Invoker.ShouldNotBeNull();
                    order.Add("ready");
                }
                return Task.FromResult(NavigationGuardResult.Allow);
            });
            await using TestApplication application = new(order);
            application.UseRouter(router);

            await application.StartAsync();

            application.Context.IsRunning.ShouldBeTrue();
            order.ShouldBe(["ready", "initialize", "resolve", "mount"]);
            BrowserObjectEvents.Invoker.ShouldNotBeNull();

            await application.StopAsync();

            order.ShouldBe(["ready", "initialize", "resolve", "mount", "unmount"]);
            BrowserObjectEvents.Invoker.ShouldBeNull();
            (await router.Push("/next")).ShouldBeNull();
            navigationCount.ShouldBe(2);
        }
        finally
        {
            RouterLinkDomBridge.Uninstall();
            BrowserObjectEvents.Invoker = previous;
        }
    }

    [Fact]
    public async Task UseRouter_DownstreamFailureAlwaysUninstallsBridge()
    {
        BrowserObjectEventInvoker? previous = BrowserObjectEvents.Invoker;
        try
        {
            BrowserObjectEvents.Invoker = null;
            using Router router = new(
                RouterHistory.CreateMemory(),
                [new RouteRecord("/")]);
            await using TestApplication application = new([]);
            application.UseRouter(router);
            application.Use(static (_, _) =>
                throw new InvalidOperationException("downstream failure"));

            InvalidOperationException exception =
                await Should.ThrowAsync<InvalidOperationException>(
                    () => application.StartAsync().AsTask());

            exception.Message.ShouldBe("downstream failure");
            BrowserObjectEvents.Invoker.ShouldBeNull();
            application.Mounted.Task.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            RouterLinkDomBridge.Uninstall();
            BrowserObjectEvents.Invoker = previous;
        }
    }

    // --- end-to-end through the real RouterLink --------------------------------------------------

    [Fact]
    public void PlainLeftClick_NavigatesClientSide_AndPreventsDefault()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, "/users/1");
        router.CurrentRoute.Value.Path.ShouldBe("/");

        var browserEvent = Click(button: 0);
        RouterLinkDomBridge.Invoke(ClickListener(wrapper), browserEvent);

        router.CurrentRoute.Value.Path.ShouldBe("/users/1"); // client-side navigation happened
        browserEvent.ToResponseFlags().ShouldBe(2);          // ...and the full page load was suppressed
    }

    [Theory]
    [InlineData(1, BrowserEventModifiers.None)]    // middle button
    [InlineData(2, BrowserEventModifiers.None)]    // right button
    [InlineData(0, BrowserEventModifiers.Control)] // ctrl+click
    [InlineData(0, BrowserEventModifiers.Meta)]    // cmd/win+click
    public void ModifiedOrNonPrimaryClick_FallsThroughToTheBrowser(int button, BrowserEventModifiers modifiers)
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, "/users/1");

        var browserEvent = Click(button: button, modifiers: modifiers);
        RouterLinkDomBridge.Invoke(ClickListener(wrapper), browserEvent);

        router.CurrentRoute.Value.Path.ShouldBe("/"); // no client-side navigation
        browserEvent.ToResponseFlags().ShouldBe(0);   // default not prevented
    }

    [Fact]
    public void AlreadyPreventedLeftClick_FallsThroughToTheBrowser()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, "/users/1");

        var browserEvent = Click(button: 0, defaultPrevented: true);
        RouterLinkDomBridge.Invoke(ClickListener(wrapper), browserEvent);

        router.CurrentRoute.Value.Path.ShouldBe("/"); // guard bailed on the arrival prevent
        browserEvent.ToResponseFlags().ShouldBe(0);   // already prevented; not re-signaled
    }

    // Runs the bridge with a capturing handler and returns the RouterLinkClickEvent it synthesized.
    private static RouterLinkClickEvent Bridge(BrowserEvent browserEvent)
    {
        RouterLinkClickEvent? captured = null;
        RouterLinkDomBridge.Invoke(value => captured = (RouterLinkClickEvent)value!, browserEvent);
        return captured!;
    }

    // A synthesized click; the BrowserEvent constructor is internal (production events come only from
    // the dispatch [JSExport]), reached here through Browser's InternalsVisibleTo.
    private static BrowserEvent Click(
        int button = 0,
        BrowserEventModifiers modifiers = BrowserEventModifiers.None,
        bool defaultPrevented = false)
        => new("click", 0, string.Empty, string.Empty, modifiers, button, 0, 0, 0, 1, true, null, false, null, defaultPrevented);

    private static Action<object?> ClickListener(ComponentWrapper wrapper)
        => (Action<object?>)wrapper.Get("a").Element.EventListeners["click"];

    private static Router LinkRouter()
        => new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/", name: "home"),
                new RouteRecord("/users", children:
                [
                    new RouteRecord(":id"),
                ]),
            ]);

    private static ComponentWrapper MountLink(Router router, string to)
    {
        var options = new ComponentMountOptions
        {
            Services = new RouterServiceProvider(router),
            Arguments = new ComponentArguments(
                [
                    new KeyValuePair<string, object?>("to", to),
                ]),
            Slots = new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
            {
                ["default"] = _ => ComponentTree.Text("link"),
            },
        };
        return ViuTest.Mount(new RouterLink(), options);
    }

    private sealed class RouterServiceProvider : IServiceProvider
    {
        private readonly Router _router;

        internal RouterServiceProvider(Router router)
        {
            _router = router;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(Router) ? _router : null;
        }
    }

    private sealed class TestApplication : IApplication
    {
        private readonly TestApplicationContext _context;
        private readonly List<ApplicationMiddleware> _middleware = [];
        private readonly List<string> _order;
        private readonly TaskCompletionSource _startup =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _stoppingSource = new();
        private Task? _execution;
        private bool _hasStarted;
        private bool _isDisposed;

        internal TestApplication(List<string> order)
        {
            _order = order;
            _context = new TestApplicationContext(
                ComponentTree.Element("main"),
                new ComponentFactory(Array.Empty<ComponentRegistration>()),
                new EmptyServiceProvider(),
                _stoppingSource.Token);
        }

        public IApplicationContext Context => _context;

        internal TaskCompletionSource Mounted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IApplication Use(ApplicationMiddleware middleware)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ArgumentNullException.ThrowIfNull(middleware);
            if (_hasStarted)
            {
                throw new InvalidOperationException(
                    "Application middleware must be registered before execution begins.");
            }

            _middleware.Add(middleware);
            return this;
        }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_hasStarted)
            {
                throw new InvalidOperationException(
                    "An application instance can execute only once.");
            }

            _hasStarted = true;
            _execution = ExecuteAsync(cancellationToken);
            return new ValueTask(_startup.Task);
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_hasStarted)
            {
                return ValueTask.CompletedTask;
            }

            _context.IsRunning = false;
            _stoppingSource.Cancel();
            Task execution = _execution ?? Task.CompletedTask;
            return cancellationToken.CanBeCanceled
                ? new ValueTask(execution.WaitAsync(cancellationToken))
                : new ValueTask(execution);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // The test that injects a downstream failure observes it through StartAsync.
            }
            finally
            {
                _stoppingSource.Dispose();
            }
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            using CancellationTokenRegistration registration =
                cancellationToken.CanBeCanceled
                    ? cancellationToken.Register(_stoppingSource.Cancel)
                    : default;
            ApplicationDelegate pipeline = ExecuteTerminalAsync;
            for (int index = _middleware.Count - 1; index >= 0; index--)
            {
                ApplicationMiddleware middleware = _middleware[index];
                ApplicationDelegate next = pipeline;
                pipeline = context => middleware(context, next);
            }

            try
            {
                await pipeline(_context).ConfigureAwait(false);
                _startup.TrySetResult();
            }
            catch (OperationCanceledException exception)
                when (_stoppingSource.IsCancellationRequested &&
                    exception.CancellationToken == _stoppingSource.Token)
            {
                _startup.TrySetResult();
            }
            catch (Exception exception)
            {
                _context.IsRunning = false;
                _stoppingSource.Cancel();
                _startup.TrySetException(exception);
                throw;
            }
        }

        private async ValueTask ExecuteTerminalAsync(IApplicationContext context)
        {
            context.Stopping.ThrowIfCancellationRequested();
            _order.Add("initialize");
            context.Stopping.ThrowIfCancellationRequested();
            _order.Add("resolve");
            context.Stopping.ThrowIfCancellationRequested();
            _order.Add("mount");
            Mounted.TrySetResult();
            _context.IsRunning = true;
            _startup.TrySetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, context.Stopping)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == context.Stopping)
            {
            }
            finally
            {
                _context.IsRunning = false;
                _order.Add("unmount");
            }
        }
    }

    private sealed class TestApplicationContext : IApplicationContext
    {
        internal TestApplicationContext(
            IComponent rootComponent,
            IComponentFactory components,
            IServiceProvider services,
            CancellationToken stopping)
        {
            RootComponent = rootComponent;
            Components = components;
            Services = services;
            Stopping = stopping;
        }

        public IComponent RootComponent { get; }

        public IComponentFactory Components { get; }

        public IServiceProvider Services { get; }

        public IStateStoreRegistry? State => null;

        public IDirectiveResolver? Directives => null;

        public Action<Exception, IComponentContext?, string>? ErrorHandler => null;

        public Action<string>? WarnHandler => null;

        public bool IsRunning { get; internal set; }

        public CancellationToken Stopping { get; }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }
}
