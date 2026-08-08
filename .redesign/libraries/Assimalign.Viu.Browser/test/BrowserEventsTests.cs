using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// Pins the qualified Browser event-guard ABI specified by [SFC-CG-2] and [V01.01.15.02].
public sealed class BrowserEventsTests
{
    [Fact]
    public void WithModifiers_StopPreventAndSelf_ApplyInSourceOrder()
    {
        int invocationCount = 0;
        Action<BrowserEvent> guarded = BrowserEvents.WithModifiers(
            (BrowserEvent _) => invocationCount++,
            "stop",
            "prevent",
            "self");
        BrowserEvent bubbledEvent = CreateEvent(isSelfTarget: false);

        guarded(bubbledEvent);

        invocationCount.ShouldBe(0);
        bubbledEvent.PropagationStopped.ShouldBeTrue();
        bubbledEvent.DefaultPrevented.ShouldBeTrue();
        bubbledEvent.ToResponseFlags().ShouldBe(3);
    }

    [Fact]
    public void WithModifiers_Exact_RejectsAnUnlistedPressedModifier()
    {
        int invocationCount = 0;
        Action<BrowserEvent> guarded = BrowserEvents.WithModifiers(
            (BrowserEvent _) => invocationCount++,
            "ctrl",
            "exact");

        guarded(CreateEvent(modifiers: BrowserEventModifiers.Control));
        guarded(CreateEvent(
            modifiers: BrowserEventModifiers.Control | BrowserEventModifiers.Shift));

        invocationCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("left", 0, true)]
    [InlineData("left", 2, false)]
    [InlineData("middle", 1, true)]
    [InlineData("right", 2, true)]
    public void WithModifiers_MouseButtons_GuardTheHandler(
        string modifier,
        int button,
        bool expectedInvocation)
    {
        bool invoked = false;
        Action<BrowserEvent> guarded = BrowserEvents.WithModifiers(
            (BrowserEvent _) => invoked = true,
            modifier);

        guarded(CreateEvent(button: button));

        invoked.ShouldBe(expectedInvocation);
    }

    [Theory]
    [InlineData("esc", "Escape")]
    [InlineData("space", " ")]
    [InlineData("up", "ArrowUp")]
    [InlineData("down", "ArrowDown")]
    [InlineData("left", "ArrowLeft")]
    [InlineData("right", "ArrowRight")]
    [InlineData("delete", "Backspace")]
    [InlineData("page-down", "PageDown")]
    public void WithKeys_KnownNamesAndAliases_MatchNormalizedBrowserKeys(
        string keyGuard,
        string browserKey)
    {
        int invocationCount = 0;
        Action<BrowserEvent> guarded = BrowserEvents.WithKeys(
            (BrowserEvent _) => invocationCount++,
            keyGuard);

        guarded(CreateEvent(eventName: "keydown", key: browserKey));

        invocationCount.ShouldBe(1);
    }

    [Fact]
    public void WithKeys_OverWithModifiers_ComposesQualifiedGuards()
    {
        List<string> keys = [];
        Action<BrowserEvent> guarded = BrowserEvents.WithKeys(
            BrowserEvents.WithModifiers(
                (BrowserEvent browserEvent) => keys.Add(browserEvent.Key),
                "ctrl"),
            "enter");

        guarded(CreateEvent(eventName: "keydown", key: "Enter"));
        guarded(CreateEvent(
            eventName: "keydown",
            key: "Enter",
            modifiers: BrowserEventModifiers.Control));

        keys.ShouldBe(["Enter"]);
    }

    [Fact]
    public void WithModifiers_ValueReturningAndParameterlessOverloads_DiscardValues()
    {
        int invocationCount = 0;
        Action<BrowserEvent> eventHandler = BrowserEvents.WithModifiers(
            (BrowserEvent _) => (object?)++invocationCount,
            "prevent");
        Action<BrowserEvent> parameterlessHandler = BrowserEvents.WithModifiers(
            () => (object?)++invocationCount,
            "prevent");

        eventHandler(CreateEvent());
        parameterlessHandler(CreateEvent());

        invocationCount.ShouldBe(2);
    }

    [Fact]
    public async Task WithKeys_TaskOverloads_PreserveMatchingTasksAndSkipNonmatchingHandlers()
    {
        TaskCompletionSource eventCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource parameterlessCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int parameterlessInvocationCount = 0;
        Func<BrowserEvent, Task> eventHandler = BrowserEvents.WithKeys(
            (BrowserEvent _) => eventCompletion.Task,
            "enter");
        Func<BrowserEvent, Task> parameterlessHandler = BrowserEvents.WithKeys(
            () =>
            {
                parameterlessInvocationCount++;
                return parameterlessCompletion.Task;
            },
            "enter");

        Task matchingEventTask = eventHandler(CreateEvent(eventName: "keyup", key: "Enter"));
        Task skippedTask = parameterlessHandler(CreateEvent(eventName: "keyup", key: "Escape"));

        matchingEventTask.ShouldBeSameAs(eventCompletion.Task);
        skippedTask.ShouldBeSameAs(Task.CompletedTask);
        parameterlessInvocationCount.ShouldBe(0);
        await skippedTask;
    }

    [Fact]
    public void BrowserEvent_DefaultPreventionSeparatesArrivalStateFromResponseIntent()
    {
        BrowserEvent browserEvent = CreateEvent(defaultPrevented: true);

        browserEvent.DefaultPrevented.ShouldBeTrue();
        browserEvent.ToResponseFlags().ShouldBe(0);

        browserEvent.PreventDefault();

        browserEvent.ToResponseFlags().ShouldBe(2);
    }

    private static BrowserEvent CreateEvent(
        string eventName = "click",
        string key = "",
        BrowserEventModifiers modifiers = BrowserEventModifiers.None,
        int button = 0,
        bool isSelfTarget = true,
        bool defaultPrevented = false) =>
        new(
            eventName,
            100,
            key,
            string.Empty,
            modifiers,
            button,
            0,
            0,
            0,
            1,
            isSelfTarget,
            null,
            false,
            defaultPrevented: defaultPrevented);
}
