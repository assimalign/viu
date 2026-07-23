using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Browser.Tests;

// Pins the DOM <Transition> CSS-class choreography through the deterministic in-memory adapter — the
// enter/leave class add/remove sequence, the forced reflow, the next-frame to-class swap, appear, and
// cancellation. Upstream contract: @vue/runtime-dom components/Transition.ts resolveTransitionProps
// (https://vuejs.org/guide/built-ins/transition.html). Real-browser end-detection is the e2e harness.
public sealed class TransitionTests : IDisposable
{
    private readonly TransitionTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Enter_AppliesTheFullClassSequence_ThroughTheAdapter()
    {
        Reference<bool> show = Reactive.Reference(false);
        _harness.Render(Host(show, ("name", "fade")));

        // Toggle in: from+active classes land immediately (onBeforeEnter), then the next frame swaps
        // from -> to, then the transition end removes to+active (upstream enter choreography).
        show.Value = true;
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.ClassLog.ShouldBe(["add:fade-enter-from", "add:fade-enter-active"]);
        _harness.Classes(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);

        _harness.AdvanceFrame();
        _harness.ClassLog[^2].ShouldBe("remove:fade-enter-from");
        _harness.ClassLog[^1].ShouldBe("add:fade-enter-to");
        _harness.Classes(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);

        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
    }

    [Fact]
    public void Leave_AppliesTheFullClassSequence_AndForcesReflow_BeforeRemoval()
    {
        Reference<bool> show = Reactive.Reference(true);
        _harness.Render(Host(show, ("name", "fade")));
        var div = _harness.FindElement("div");
        // No appear -> the initial mount runs no enter choreography.
        _harness.ClassLog.ShouldBeEmpty();

        show.Value = false;
        _harness.RunUntilIdle();
        // from+active land, and a reflow is forced between them (upstream onLeave).
        _harness.Classes(div).ShouldBe(["fade-leave-from", "fade-leave-active"], ignoreOrder: true);
        _harness.ReflowCount.ShouldBe(1);
        _harness.IsMounted(div).ShouldBeTrue(); // removal deferred behind the leave

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-leave-active", "fade-leave-to"], ignoreOrder: true);

        _harness.FireTransitionEnd(div);
        _harness.IsMounted(div).ShouldBeFalse(); // removed only once the leave completed
    }

    [Fact]
    public void Appear_RunsEnterChoreographyOnInitialMount()
    {
        Reference<bool> show = Reactive.Reference(true);
        _harness.Render(Host(show, ("name", "fade"), ("appear", true)));

        // appear -> the enter (appear) classes are applied on the very first mount.
        var div = _harness.FindElement("div");
        _harness.Classes(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);
        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);
        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
    }

    [Fact]
    public void LeaveInterruptingEnter_CancelsTheEnter_AndAppliesLeaveClasses()
    {
        Reference<bool> show = Reactive.Reference(false);
        _harness.Render(Host(show, ("name", "fade")));

        // Start entering, but do NOT advance the frame or fire the end.
        show.Value = true;
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Classes(div).ShouldContain("fade-enter-active");

        // Interrupt with a leave before the enter finishes: the enter is cancelled (its active class is
        // removed by finishEnter) and the leave classes take over (upstream el[enterCbKey](true)).
        show.Value = false;
        _harness.RunUntilIdle();
        _harness.Classes(div).ShouldNotContain("fade-enter-active");
        _harness.Classes(div).ShouldContain("fade-leave-from");
        _harness.Classes(div).ShouldContain("fade-leave-active");

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldNotContain("fade-enter-to");
    }

    [Fact]
    public void CssFalse_SkipsAllClassAndEndDetectionWork()
    {
        Reference<bool> show = Reactive.Reference(false);
        _harness.Render(Host(show, ("name", "fade"), ("css", false)));

        show.Value = true;
        _harness.RunUntilIdle();
        _harness.AdvanceFrame();

        // :css="false" -> no transition classes and no reflow are ever applied (upstream returns baseProps).
        _harness.ClassLog.ShouldBeEmpty();
        _harness.ReflowCount.ShouldBe(0);
        var div = _harness.FindElement("div");
        _harness.IsMounted(div).ShouldBeTrue();
    }

    [Fact]
    public void SynchronousEnterHook_DoesNotCompleteTheCssTransition()
    {
        Reference<bool> show = Reactive.Reference(false);
        int invocationCount = 0;
        _harness.Render(
            Host(
                show,
                ("name", "fade"),
                ("onEnter", (Action<object>)(_ => invocationCount++))));

        show.Value = true;
        _harness.RunUntilIdle();

        int div = _harness.FindElement("div");
        invocationCount.ShouldBe(1);
        _harness.Classes(div).ShouldBe(
            ["fade-enter-from", "fade-enter-active"],
            ignoreOrder: true);

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(
            ["fade-enter-active", "fade-enter-to"],
            ignoreOrder: true);
        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
    }

    // A component rendering <Transition {props}> around a v-if div keyed "a".
    private static ITemplateComponent Host(
        Reference<bool> show,
        params (string Name, object? Value)[] transitionProperties)
    {
        List<KeyValuePair<string, object?>> arguments =
            new(transitionProperties.Length);
        foreach ((string name, object? value) in transitionProperties)
        {
            arguments.Add(new KeyValuePair<string, object?>(name, value));
        }

        Dictionary<string, ComponentSlot> slots =
            new(StringComparer.Ordinal)
            {
                ["default"] = _ =>
                    show.Value
                        ? ComponentTree.Element(
                            "div",
                            children: [ComponentTree.Text("A")],
                            key: "a")
                        : ComponentTree.Comment(),
            };
        return ComponentTree.Template<Transition>(
            new ComponentArguments(arguments),
            slots);
    }
}
