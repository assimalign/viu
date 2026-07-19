using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins withModifiers/withKeys parity with @vue/runtime-dom's vOn helpers —
// https://vuejs.org/guide/essentials/event-handling.html.
public class BrowserEventsTests
{
    private static BrowserEvent Event(
        string eventName = "click",
        string key = "",
        BrowserEventModifiers modifiers = BrowserEventModifiers.None,
        int button = 0,
        bool isSelfTarget = true)
        => new(eventName, 100, key, string.Empty, modifiers, button, 0, 0, 0, 1, isSelfTarget, null, false);

    [Fact]
    public void WithModifiers_StopAndPrevent_RecordIntentsAndStillRunTheHandler()
    {
        var runs = 0;
        var handler = BrowserEvents.WithModifiers(_ => runs++, "stop", "prevent");
        var browserEvent = Event();

        handler(browserEvent);

        runs.ShouldBe(1);
        browserEvent.PropagationStopped.ShouldBeTrue();
        browserEvent.DefaultPrevented.ShouldBeTrue();
        browserEvent.ToResponseFlags().ShouldBe(3);
    }

    [Fact]
    public void WithModifiers_Self_SkipsBubbledEvents()
    {
        var runs = 0;
        var handler = BrowserEvents.WithModifiers(_ => runs++, "self");

        handler(Event(isSelfTarget: false));
        runs.ShouldBe(0);

        handler(Event(isSelfTarget: true));
        runs.ShouldBe(1);
    }

    [Theory]
    [InlineData("ctrl", BrowserEventModifiers.Control)]
    [InlineData("shift", BrowserEventModifiers.Shift)]
    [InlineData("alt", BrowserEventModifiers.Alt)]
    [InlineData("meta", BrowserEventModifiers.Meta)]
    public void WithModifiers_SystemModifiers_GateOnTheKeyState(string modifier, BrowserEventModifiers flag)
    {
        var runs = 0;
        var handler = BrowserEvents.WithModifiers(_ => runs++, modifier);

        handler(Event());
        runs.ShouldBe(0);

        handler(Event(modifiers: flag));
        runs.ShouldBe(1);

        // Non-exact: extra modifiers still pass.
        handler(Event(modifiers: flag | BrowserEventModifiers.Shift | BrowserEventModifiers.Control));
        runs.ShouldBe(2);
    }

    [Fact]
    public void WithModifiers_MouseButtons_GateOnTheButton()
    {
        var leftRuns = 0;
        var rightRuns = 0;
        var middleRuns = 0;
        var left = BrowserEvents.WithModifiers(_ => leftRuns++, "left");
        var right = BrowserEvents.WithModifiers(_ => rightRuns++, "right");
        var middle = BrowserEvents.WithModifiers(_ => middleRuns++, "middle");

        left(Event(button: 0));
        left(Event(button: 2));
        right(Event(button: 2));
        right(Event(button: 0));
        middle(Event(button: 1));
        middle(Event(button: 0));

        leftRuns.ShouldBe(1);
        rightRuns.ShouldBe(1);
        middleRuns.ShouldBe(1);
    }

    [Fact]
    public void WithModifiers_Exact_RequiresExactlyTheListedSystemModifiers()
    {
        var runs = 0;
        var handler = BrowserEvents.WithModifiers(_ => runs++, "ctrl", "exact");

        handler(Event(modifiers: BrowserEventModifiers.Control));
        runs.ShouldBe(1);

        // An extra pressed modifier fails .exact.
        handler(Event(modifiers: BrowserEventModifiers.Control | BrowserEventModifiers.Shift));
        runs.ShouldBe(1);

        // .exact with no system modifiers listed: none may be pressed.
        var bare = BrowserEvents.WithModifiers(_ => runs++, "exact");
        bare(Event());
        runs.ShouldBe(2);
        bare(Event(modifiers: BrowserEventModifiers.Meta));
        runs.ShouldBe(2);
    }

    [Fact]
    public void WithKeys_MatchesPlainKeysCaseInsensitively()
    {
        var runs = 0;
        var handler = BrowserEvents.WithKeys(_ => runs++, "enter");

        handler(Event("keydown", key: "Enter"));
        runs.ShouldBe(1);

        handler(Event("keydown", key: "Tab"));
        runs.ShouldBe(1);
    }

    [Theory]
    [InlineData("esc", "Escape")]
    [InlineData("up", "ArrowUp")]
    [InlineData("down", "ArrowDown")]
    [InlineData("left", "ArrowLeft")]
    [InlineData("right", "ArrowRight")]
    [InlineData("delete", "Backspace")]
    [InlineData("tab", "Tab")]
    [InlineData("space", " ")]
    public void WithKeys_MatchesVueKeyAliases(string alias, string domKey)
    {
        var runs = 0;
        var handler = BrowserEvents.WithKeys(_ => runs++, alias);

        handler(Event("keydown", key: domKey));

        runs.ShouldBe(1);
    }

    [Fact]
    public void WithKeys_IgnoresNonKeyboardEvents()
    {
        var runs = 0;
        var handler = BrowserEvents.WithKeys(_ => runs++, "enter");

        handler(Event("click", key: ""));

        runs.ShouldBe(0);
    }

    [Fact]
    public void WithModifiers_ComposesWithWithKeys()
    {
        var seen = new List<string>();
        var handler = BrowserEvents.WithKeys(
            BrowserEvents.WithModifiers(browserEvent => seen.Add(browserEvent.Key), "ctrl"),
            "enter");

        handler(Event("keydown", key: "Enter"));
        handler(Event("keydown", key: "Enter", modifiers: BrowserEventModifiers.Control));

        seen.ShouldBe(["Enter"]);
    }
}
