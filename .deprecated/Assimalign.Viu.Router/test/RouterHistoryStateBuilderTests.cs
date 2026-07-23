using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the push/replace/bootstrap state arithmetic — the C# port of vue-router's buildState and the
// state assembly in useHistoryStateNavigation (packages/router/src/history/html5.ts). [V01.01.08.02]
// position-counter round-trip AC.
public class RouterHistoryStateBuilderTests
{
    [Fact]
    public void BuildInitial_SeedsAReplacedRootedEntry()
    {
        var state = RouterHistoryStateBuilder.BuildInitial("/start", position: 5);

        state.Back.ShouldBeNull();
        state.Current.ShouldBe("/start");
        state.Forward.ShouldBeNull();
        state.Replaced.ShouldBeTrue();
        state.Position.ShouldBe(5);
        state.Scroll.ShouldBeNull();
    }

    [Fact]
    public void AmendCurrentForPush_OnlyRepointsForward()
    {
        var current = new RouterHistoryState("/back", "/current", null, Replaced: false, Position: 2, Scroll: null);

        var amended = RouterHistoryStateBuilder.AmendCurrentForPush(current, "/next");

        // Forward now points at the pushed location; every other field is preserved (scroll is filled
        // in by the interop at apply time, not here).
        amended.Forward.ShouldBe("/next");
        amended.Back.ShouldBe("/back");
        amended.Current.ShouldBe("/current");
        amended.Position.ShouldBe(2);
    }

    [Fact]
    public void BuildForPush_LinksBackAndAdvancesPositionByOne()
    {
        var current = new RouterHistoryState("/older", "/current", null, Replaced: false, Position: 3, Scroll: null);

        var pushed = RouterHistoryStateBuilder.BuildForPush(current, "/next", scrollSeed: null);

        pushed.Back.ShouldBe("/current");   // the entry we are leaving
        pushed.Current.ShouldBe("/next");
        pushed.Forward.ShouldBeNull();
        pushed.Replaced.ShouldBeFalse();
        pushed.Position.ShouldBe(4);         // +1, the monotonic advance
        pushed.Scroll.ShouldBeNull();
    }

    [Fact]
    public void BuildForPush_SeedsScrollWhenProvided()
    {
        var current = new RouterHistoryState(null, "/current", null, Replaced: true, Position: 0, Scroll: null);

        var pushed = RouterHistoryStateBuilder.BuildForPush(current, "/next", new ScrollPosition(10, 20));

        pushed.Scroll.ShouldBe(new ScrollPosition(10, 20));
    }

    [Fact]
    public void BuildForReplace_KeepsNeighboursAndPosition()
    {
        var current = new RouterHistoryState("/back", "/current", "/forward", Replaced: false, Position: 7, Scroll: null);

        var replaced = RouterHistoryStateBuilder.BuildForReplace(current, "/swap", scrollSeed: null);

        replaced.Back.ShouldBe("/back");        // neighbours preserved
        replaced.Forward.ShouldBe("/forward");
        replaced.Current.ShouldBe("/swap");
        replaced.Replaced.ShouldBeTrue();
        replaced.Position.ShouldBe(7);          // position preserved, not advanced
    }
}
