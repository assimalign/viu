using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the memory history — the C# port of vue-router's createMemoryHistory
// (packages/router/src/history/memory.ts). It is the interop-free reference model for the
// push/replace/go and position semantics the web history reproduces. [V01.01.08.02] memory-mode AC.
public class MemoryRouterHistoryTests
{
    private static IRouterHistory CreateHistory(string? basePath = null)
        => RouterHistory.CreateMemory(basePath);

    [Fact]
    public void InitialEntry_IsRootAtPositionZero()
    {
        var history = CreateHistory();

        history.Location.ShouldBe("/");
        history.State.Current.ShouldBe("/");
        history.State.Position.ShouldBe(0);
        history.State.Replaced.ShouldBeTrue();
        history.Base.ShouldBe("");
    }

    [Fact]
    public void Push_AdvancesLocationAndPositionAndLinksBack()
    {
        var history = CreateHistory();

        history.Push("/a");
        history.Push("/b");

        history.Location.ShouldBe("/b");
        history.State.Position.ShouldBe(2);   // monotonic +1 per push
        history.State.Back.ShouldBe("/a");    // linked to the entry left behind
    }

    [Fact]
    public void Push_DoesNotNotifyListeners()
    {
        var history = CreateHistory();
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Push("/a");

        // Only pop navigations (go/back/forward) notify — pushes do not.
        notifications.ShouldBe(0);
    }

    [Fact]
    public void GoBack_MovesToPreviousEntryAndNotifiesWithBackDirection()
    {
        var history = CreateHistory();
        history.Push("/a");
        history.Push("/b");
        var seen = new List<NavigationInformation>();
        var toFrom = new List<(string To, string From)>();
        history.Listen((to, from, information) =>
        {
            seen.Add(information);
            toFrom.Add((to, from));
        });

        history.Go(-1);

        history.Location.ShouldBe("/a");
        history.State.Position.ShouldBe(1);
        seen.Count.ShouldBe(1);
        seen[0].Type.ShouldBe(NavigationType.Pop);
        seen[0].Direction.ShouldBe(NavigationDirection.Back);
        seen[0].Delta.ShouldBe(-1);
        toFrom[0].ShouldBe(("/a", "/b"));
    }

    [Fact]
    public void GoForward_AfterGoingBack_MovesForwardWithForwardDirection()
    {
        var history = CreateHistory();
        history.Push("/a");
        history.Push("/b");
        history.Go(-2);   // back to "/"
        var seen = new List<NavigationInformation>();
        history.Listen((to, from, information) => seen.Add(information));

        history.Go(1);

        history.Location.ShouldBe("/a");
        seen[0].Direction.ShouldBe(NavigationDirection.Forward);
        seen[0].Delta.ShouldBe(1);
    }

    [Fact]
    public void Go_ClampsWithinBoundsButStillNotifiesWithRawDelta()
    {
        var history = CreateHistory();
        history.Push("/a");   // position 1, at the tip
        var seen = new List<NavigationInformation>();
        history.Listen((to, from, information) => seen.Add(information));

        history.Go(5);        // clamps: cannot move past the tip

        history.Location.ShouldBe("/a");     // unchanged
        seen[0].Delta.ShouldBe(5);           // raw requested delta, upstream-faithful
        seen[0].Direction.ShouldBe(NavigationDirection.Forward);
    }

    [Fact]
    public void Go_ZeroDelta_IsForwardInAbstractMode()
    {
        var history = CreateHistory();
        var seen = new List<NavigationInformation>();
        history.Listen((to, from, information) => seen.Add(information));

        history.Go(0);

        // Upstream treats delta === 0 as forward in abstract mode (memory has no page reload).
        seen[0].Direction.ShouldBe(NavigationDirection.Forward);
    }

    [Fact]
    public void Go_WithTriggerListenersFalse_DoesNotNotify()
    {
        var history = CreateHistory();
        history.Push("/a");
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Go(-1, triggerListeners: false);

        history.Location.ShouldBe("/");   // still moved
        notifications.ShouldBe(0);        // but silently
    }

    [Fact]
    public void PushAfterGoingBack_TruncatesForwardEntries()
    {
        var history = CreateHistory();
        history.Push("/a");
        history.Push("/b");
        history.Go(-1);       // back to "/a"; "/b" is now a forward entry

        history.Push("/c");   // replaces the forward branch

        history.Location.ShouldBe("/c");
        history.State.Position.ShouldBe(2);
        history.Go(1);        // there is nothing forward of "/c"
        history.Location.ShouldBe("/c");
    }

    [Fact]
    public void Replace_KeepsPositionAndTruncatesForward()
    {
        var history = CreateHistory();
        history.Push("/a");
        history.Push("/b");
        history.Go(-1);       // at "/a", position 1, "/b" forward

        history.Replace("/x");

        history.Location.ShouldBe("/x");
        history.State.Position.ShouldBe(1);   // replace preserves the position counter
        history.State.Replaced.ShouldBeTrue();
        history.Go(1);
        history.Location.ShouldBe("/x");      // "/b" was truncated by the replace
    }

    [Fact]
    public void Listen_ReturnedUnsubscribe_StopsFurtherNotifications()
    {
        var history = CreateHistory();
        history.Push("/a");
        var notifications = 0;
        var unsubscribe = history.Listen((to, from, information) => notifications++);

        history.Go(-1);
        unsubscribe();
        history.Go(1);

        notifications.ShouldBe(1);   // only the first go was observed
    }

    [Fact]
    public void Destroy_ResetsToRootAndClearsListeners()
    {
        var history = CreateHistory();
        history.Push("/a");
        var notifications = 0;
        history.Listen((to, from, information) => notifications++);

        history.Destroy();

        history.Location.ShouldBe("/");
        history.State.Position.ShouldBe(0);
        history.Push("/b");
        history.Go(-1);
        notifications.ShouldBe(0);   // the pre-destroy listener is gone
    }

    [Theory]
    [InlineData(null, "/users", "/users")]     // no base
    [InlineData("/app/", "/users", "/app/users")]
    public void CreateHref_PrefixesTheBase(string? basePath, string location, string expected)
        => CreateHistory(basePath).CreateHref(location).ShouldBe(expected);

    [Fact]
    public void CreateMemory_NormalizesTheBase()
        => CreateHistory("/app/").Base.ShouldBe("/app");
}
