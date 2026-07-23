using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the browser history policy (BrowserRouterHistory) — the C# port of vue-router's
// useHistoryStateNavigation + useHistoryListeners (packages/router/src/history/html5.ts) — exercised
// against the instrumented interop seam with no browser. Covers base prepend-on-write /
// strip-on-read, the position-counter state round-trip, popstate delta/direction, the batched-read
// call count, silent go, and listener teardown. [V01.01.08.02] browser-mode ACs.
public class BrowserRouterHistoryTests
{
    private static BrowserHistorySnapshot Snapshot(
        string pathname,
        RouterHistoryState? state,
        int historyLength = 1,
        string search = "",
        string hash = "",
        string host = "example.com")
        => new(pathname, search, hash, host, historyLength, state);

    private static RouterHistoryState StateAt(
        string current,
        int position,
        string? back = null,
        string? forward = null,
        bool replaced = false)
        => new(back, current, forward, replaced, position, null);

    private static (BrowserRouterHistory History, FakeBrowserHistoryInterop Interop) CreateWeb(
        string normalizedBase,
        BrowserHistorySnapshot initialSnapshot)
    {
        var interop = new FakeBrowserHistoryInterop(initialSnapshot);
        var history = new BrowserRouterHistory(interop, normalizedBase);
        return (history, interop);
    }

    [Fact]
    public void Construction_WithNoExistingState_SeedsInitialStateAndSubscribes()
    {
        // A fresh navigation to /app/users with no prior Viu state, base "/app".
        var (history, interop) = CreateWeb("/app", Snapshot("/app/users", state: null, historyLength: 6));

        history.Location.ShouldBe("/users");             // base stripped on read
        history.State.Position.ShouldBe(5);              // seeded from historyLength - 1
        history.State.Replaced.ShouldBeTrue();
        interop.ReplaceCalls.Count.ShouldBe(1);          // the bootstrap replaceState
        interop.ReplaceCalls[0].ToUrl.ShouldBe("/app/users");  // written with the base prepended
        interop.IsSubscribed.ShouldBeTrue();
        interop.SubscribeCount.ShouldBe(1);
    }

    [Fact]
    public void Construction_WithExistingState_AdoptsItWithoutBootstrapping()
    {
        var (history, interop) = CreateWeb("", Snapshot("/users", StateAt("/users", position: 3)));

        history.State.Position.ShouldBe(3);
        interop.ReplaceCalls.ShouldBeEmpty();   // no bootstrap when state already exists
    }

    [Fact]
    public void Push_WritesBasePrefixedUrlsAndAdvancesPosition()
    {
        var (history, interop) = CreateWeb("/app", Snapshot("/", StateAt("/", position: 0), historyLength: 1));

        history.Push("/users");

        var push = interop.PushCalls.ShouldHaveSingleItem();
        push.CurrentUrl.ShouldBe("/app/");          // the leaving entry's URL (base + "/")
        push.ToUrl.ShouldBe("/app/users");          // the pushed URL (base prepended)
        push.AmendedCurrent.Forward.ShouldBe("/users");  // leaving entry repointed forward
        push.NewState.Position.ShouldBe(1);         // monotonic advance
        history.Location.ShouldBe("/users");        // base stripped in the exposed location
        history.State.Position.ShouldBe(1);
    }

    [Fact]
    public void Push_InHashMode_WritesHashFragmentUrls()
    {
        // Hash base "#": BuildUrl uses the "#…" slice, so navigation only touches the fragment.
        var (history, interop) = CreateWeb("#", Snapshot("/", StateAt("/", position: 0)));

        history.Push("/users");

        interop.PushCalls[0].ToUrl.ShouldBe("#/users");   // never a server-request path
    }

    [Fact]
    public void Replace_WritesBasePrefixedUrlAndPreservesPosition()
    {
        var (history, interop) = CreateWeb("/app", Snapshot("/users", StateAt("/users", position: 4)));

        history.Replace("/profile");

        var replace = interop.ReplaceCalls.ShouldHaveSingleItem();
        replace.ToUrl.ShouldBe("/app/profile");
        replace.NewState.Position.ShouldBe(4);        // position preserved
        replace.NewState.Replaced.ShouldBeTrue();
        history.Location.ShouldBe("/profile");
    }

    [Fact]
    public void PopState_Backward_StripsBaseAndReportsBackWithSignedDelta()
    {
        var (history, interop) = CreateWeb("/app", Snapshot("/b", StateAt("/b", position: 7)));
        var seen = new List<(string To, string From, NavigationInformation Information)>();
        history.Listen((to, from, information) => seen.Add((to, from, information)));

        // Browser back to /app/a whose stored state is at position 6.
        interop.FirePopState(Snapshot("/app/a", StateAt("/a", position: 6)));

        history.Location.ShouldBe("/a");         // base stripped on read
        history.State.Position.ShouldBe(6);
        var (to, from, information) = seen.ShouldHaveSingleItem();
        to.ShouldBe("/a");
        from.ShouldBe("/b");
        information.Type.ShouldBe(NavigationType.Pop);
        information.Direction.ShouldBe(NavigationDirection.Back);
        information.Delta.ShouldBe(-1);          // 6 - 7
    }

    [Fact]
    public void PopState_Forward_ReportsForwardDirection()
    {
        var (history, interop) = CreateWeb("", Snapshot("/a", StateAt("/a", position: 3)));
        var seen = new List<NavigationInformation>();
        history.Listen((to, from, information) => seen.Add(information));

        interop.FirePopState(Snapshot("/b", StateAt("/b", position: 5)));

        seen[0].Direction.ShouldBe(NavigationDirection.Forward);
        seen[0].Delta.ShouldBe(2);   // 5 - 3
    }

    [Fact]
    public void PopState_SamePosition_ReportsUnknownDirection()
    {
        var (history, interop) = CreateWeb("", Snapshot("/a", StateAt("/a", position: 3)));
        var seen = new List<NavigationInformation>();
        history.Listen((to, from, information) => seen.Add(information));

        interop.FirePopState(Snapshot("/a", StateAt("/a", position: 3)));

        seen[0].Direction.ShouldBe(NavigationDirection.Unknown);
        seen[0].Delta.ShouldBe(0);
    }

    [Fact]
    public void PopState_WithNoState_SynthesizesByReplacing()
    {
        var (history, interop) = CreateWeb("", Snapshot("/a", StateAt("/a", position: 2)));
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        // An entry created before this history (no Viu state) arrives via popstate.
        interop.FirePopState(Snapshot("/legacy", state: null));

        interop.ReplaceCalls.ShouldHaveSingleItem();   // synthesized a state in place
        history.Location.ShouldBe("/legacy");
        notifications.ShouldBe(1);
    }

    [Fact]
    public void SilentGo_SwallowsTheResultingPopStateButStillUpdatesState()
    {
        var (history, interop) = CreateWeb("", Snapshot("/b", StateAt("/b", position: 2)));
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Go(-1, triggerListeners: false);
        interop.GoCalls.ShouldBe([-1]);
        // The browser now fires popstate for the arrived entry; the policy must swallow it.
        interop.FirePopState(Snapshot("/a", StateAt("/a", position: 1)));

        notifications.ShouldBe(0);            // swallowed
        history.Location.ShouldBe("/a");      // but state is reconciled
        history.State.Position.ShouldBe(1);
    }

    [Fact]
    public void Go_WithTriggerListeners_DoesNotSwallowTheNextPopState()
    {
        var (history, interop) = CreateWeb("", Snapshot("/b", StateAt("/b", position: 2)));
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Go(-1);
        interop.FirePopState(Snapshot("/a", StateAt("/a", position: 1)));

        notifications.ShouldBe(1);
    }

    [Fact]
    public void LocationAndStateReads_AreBatchedIntoASingleInteropCall()
    {
        // The whole batched-read criterion: the policy reads the environment ONCE at construction and
        // never issues a per-property / per-navigation getter afterward.
        var (history, interop) = CreateWeb("/app", Snapshot("/", StateAt("/", position: 0)));

        history.Push("/a");
        history.Replace("/b");
        history.Go(-1);
        interop.FirePopState(Snapshot("/app/c", StateAt("/c", position: 1)));
        _ = history.Location;
        _ = history.State;

        interop.ReadSnapshotCount.ShouldBe(1);   // exactly the one bootstrap read
    }

    [Fact]
    public void Listen_ReturnedUnsubscribe_StopsNotifications()
    {
        var (history, interop) = CreateWeb("", Snapshot("/a", StateAt("/a", position: 1)));
        var notifications = 0;
        var unsubscribe = history.Listen((to, from, information) => notifications++);

        interop.FirePopState(Snapshot("/b", StateAt("/b", position: 2)));
        unsubscribe();
        interop.FirePopState(Snapshot("/c", StateAt("/c", position: 3)));

        notifications.ShouldBe(1);
    }

    [Fact]
    public void Destroy_UnsubscribesTheInteropListenerAndClearsListeners()
    {
        var (history, interop) = CreateWeb("", Snapshot("/a", StateAt("/a", position: 1)));
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Destroy();

        interop.UnsubscribeCount.ShouldBe(1);   // the JS popstate listener is torn down (no leak)
        interop.IsSubscribed.ShouldBeFalse();
        interop.FirePopState(Snapshot("/b", StateAt("/b", position: 2)));
        notifications.ShouldBe(0);
    }

    [Fact]
    public void CreateHref_UsesTheConfiguredBase()
    {
        var (history, _) = CreateWeb("/app", Snapshot("/", StateAt("/", position: 0)));

        history.CreateHref("/users").ShouldBe("/app/users");
    }
}
