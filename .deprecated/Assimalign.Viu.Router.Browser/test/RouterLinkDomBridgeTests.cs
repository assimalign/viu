using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Router.Browser.Tests;

// Pins the Router<->DOM click bridge ([V01.01.08.03.01], issue #191): the browser adapter's
// BrowserEvent is mapped onto vue-router's guardEvent contract (button + system modifiers +
// defaultPrevented, packages/router/src/RouterLink.ts, https://github.com/vuejs/router) and the
// guard's preventDefault decision is mirrored back onto the live event's response flags. DOM-free
// through the in-memory Testing renderer; real-browser behavior is the e2e harness ([V01.01.11.03]).
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
        // guard falls through (upstream guardEvent bails on e.defaultPrevented).
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
        var options = new ComponentMountOptions();
        options.Provide(RouterInjectionKeys.Router, router);
        options.Properties = VirtualNodeFactory.Properties(("to", to));
        var slots = new ComponentSlots();
        slots["default"] = _ => [VirtualNodeFactory.Text("link")];
        options.Slots = slots;
        return ViuTest.Mount(new RouterLink(), options);
    }
}
