using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

using static Assimalign.Viu.VirtualNodeFactory;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins the PERSISTED v-show transition path ([V01.01.04.07.01]) through the deterministic in-memory
// adapter: a <Transition persisted> wrapping a v-show <div> runs the enter/leave hooks + CSS classes on
// each binding toggle WITHOUT unmounting the element, captures the original display once and restores it,
// and converges a toggle-during-transition interruption to the final visibility with no orphaned classes.
// The <Transition persisted> stands in for the compiler's transformTransition injection (persisted:true
// whenever a <transition> wraps a single v-show child). Upstream contract: @vue/runtime-dom
// directives/vShow.ts persisted handling + components/Transition.ts
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vShow.ts,
// https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/components/Transition.ts).
public sealed class VShowTransitionTests : IDisposable
{
    private readonly VShowTransitionTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Show_RunsEnterChoreography_WithoutUnmountingTheElement()
    {
        var show = Reactive.Reference<object?>(false);
        _harness.Render(PersistedHost(show));
        var div = _harness.FindElement("div");
        // Initially falsy: beforeMount hides it directly (no enter/leave classes, no reflow).
        _harness.Display(div).ShouldBe("none");
        _harness.Classes(div).ShouldBeEmpty();

        // Toggle in: the enter choreography runs on SHOW (from+active now, to-swap next frame) and the
        // element is revealed — but it is the SAME element, never unmounted/remounted.
        show.Value = true;
        _harness.RunUntilIdle();
        _harness.Classes(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // revealed (no saved inline display -> removed)
        _harness.IsMounted(div).ShouldBeTrue();
        _harness.FindElement("div").ShouldBe(div); // no new element created -> not remounted

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);

        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
        _harness.Display(div).ShouldBeNull();
        _harness.IsMounted(div).ShouldBeTrue();
        // Run count: display was written exactly twice — hidden at mount, revealed on show.
        _harness.DisplayLog.ShouldBe(["none", VShowTransitionTestHarness.DisplayRemoved]);
    }

    [Fact]
    public void Hide_RunsLeaveChoreography_ThenHidesOnlyAfterItCompletes_WithoutUnmounting()
    {
        var show = Reactive.Reference<object?>(true);
        _harness.Render(PersistedHost(show));
        var div = _harness.FindElement("div");
        // Initially truthy, no appear -> no enter choreography and no display write at mount (visible).
        _harness.Classes(div).ShouldBeEmpty();
        _harness.Display(div).ShouldBeNull();
        _harness.DisplayLog.ShouldBeEmpty();

        // Toggle out: leave choreography runs on HIDE (from+active + a forced reflow), but the element
        // stays visible AND mounted until the leave completes.
        show.Value = false;
        _harness.RunUntilIdle();
        _harness.Classes(div).ShouldBe(["fade-leave-from", "fade-leave-active"], ignoreOrder: true);
        _harness.ReflowCount.ShouldBe(1);
        _harness.Display(div).ShouldBeNull(); // NOT hidden yet — the leave is still running
        _harness.IsMounted(div).ShouldBeTrue();

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-leave-active", "fade-leave-to"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // still visible mid-leave

        _harness.FireTransitionEnd(div);
        // Leave complete: classes cleared, display:none applied by the leave's done callback, element STILL
        // mounted (v-show persists it; the renderer's remove path was never taken).
        _harness.Classes(div).ShouldBeEmpty();
        _harness.Display(div).ShouldBe("none");
        _harness.IsMounted(div).ShouldBeTrue();
        _harness.DisplayLog.ShouldBe(["none"]); // hidden exactly once, after the leave
    }

    [Fact]
    public void InitiallyHidden_IsHiddenAtMount_WithNoTransitionChoreography()
    {
        var show = Reactive.Reference<object?>(false);
        _harness.Render(PersistedHost(show));
        var div = _harness.FindElement("div");
        // beforeMount hides an initially-falsy element directly (upstream: transition && value is false, so
        // setDisplay(el, false)) — no enter/leave classes and no reflow on the first mount.
        _harness.Display(div).ShouldBe("none");
        _harness.Classes(div).ShouldBeEmpty();
        _harness.ReflowCount.ShouldBe(0);
    }

    [Fact]
    public void OriginalInlineDisplay_IsCapturedOnce_AndRestoredAcrossRepeatedToggles()
    {
        var show = Reactive.Reference<object?>(false);
        _harness.Render(PersistedHost(show, inlineDisplay: "flex"));
        var div = _harness.FindElement("div");
        // Falsy at mount -> hidden; the author-supplied inline display ("flex") is captured for later.
        _harness.Display(div).ShouldBe("none");

        // Show -> the captured original "flex" is restored (not the empty/default), driven by the enter.
        show.Value = true;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("flex");
        _harness.AdvanceFrame();
        _harness.FireTransitionEnd(div);
        _harness.Display(div).ShouldBe("flex");

        // Hide -> display:none once the leave completes.
        show.Value = false;
        _harness.RunUntilIdle();
        _harness.AdvanceFrame();
        _harness.FireTransitionEnd(div);
        _harness.Display(div).ShouldBe("none");

        // Show again -> the SAME original "flex" is restored: captured once at mount, never lost to the
        // toggles (upstream vShowOriginalDisplay is set only in beforeMount).
        show.Value = true;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("flex");
        _harness.AdvanceFrame();
        _harness.FireTransitionEnd(div);
        _harness.Display(div).ShouldBe("flex");
    }

    [Fact]
    public void HideInterruptingEnter_CancelsTheEnter_ConvergesHidden_WithNoOrphanClasses()
    {
        var show = Reactive.Reference<object?>(false);
        _harness.Render(PersistedHost(show));
        var div = _harness.FindElement("div");

        // Start entering and let it reach the "to" phase (genuinely mid-transition, waiting on the end).
        show.Value = true;
        _harness.RunUntilIdle();
        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // revealed for the enter

        // Interrupt with a hide before the enter finishes: the enter is cancelled (its classes removed by
        // finishEnter, upstream el[enterCbKey](true)) and the leave classes take over, with no orphaned
        // enter class. The element is still visible and mounted — hiding waits for the leave to complete.
        show.Value = false;
        _harness.RunUntilIdle();
        _harness.Classes(div).ShouldNotContain("fade-enter-from");
        _harness.Classes(div).ShouldNotContain("fade-enter-to");
        _harness.Classes(div).ShouldNotContain("fade-enter-active");
        _harness.Classes(div).ShouldBe(["fade-leave-from", "fade-leave-active"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // still visible mid-leave

        // Complete the leave: converges hidden with no orphaned classes; the element is never unmounted.
        _harness.AdvanceFrame();
        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
        _harness.Display(div).ShouldBe("none");
        _harness.IsMounted(div).ShouldBeTrue();
        // Run count: hidden at mount, revealed for the enter, hidden again once the leave finished.
        _harness.DisplayLog.ShouldBe(["none", VShowTransitionTestHarness.DisplayRemoved, "none"]);
    }

    [Fact]
    public void ShowInterruptingLeave_CancelsTheLeave_ConvergesVisible_WithNoOrphanClasses()
    {
        var show = Reactive.Reference<object?>(true);
        _harness.Render(PersistedHost(show));
        var div = _harness.FindElement("div");

        // Start leaving and let it reach the "to" phase (genuinely mid-transition, waiting on the end).
        show.Value = false;
        _harness.RunUntilIdle();
        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-leave-active", "fade-leave-to"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // still visible while leaving

        // Interrupt with a show before the leave finishes: the leave is cancelled (its classes removed by
        // finishLeave, upstream el[leaveCbKey](true)) and the enter classes take over, with no orphaned
        // leave class.
        show.Value = true;
        _harness.RunUntilIdle();
        _harness.Classes(div).ShouldNotContain("fade-leave-from");
        _harness.Classes(div).ShouldNotContain("fade-leave-to");
        _harness.Classes(div).ShouldNotContain("fade-leave-active");
        _harness.Classes(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);
        _harness.Display(div).ShouldBeNull(); // converged visible

        _harness.AdvanceFrame();
        _harness.Classes(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);
        _harness.FireTransitionEnd(div);
        _harness.Classes(div).ShouldBeEmpty();
        _harness.Display(div).ShouldBeNull(); // visible
        _harness.IsMounted(div).ShouldBeTrue();
        // Run count: the cancelled leave's done callback wrote a transient display:none (upstream
        // done(cancelled) still calls remove), immediately overwritten by the show's reveal — final: visible.
        _harness.DisplayLog.ShouldBe(["none", VShowTransitionTestHarness.DisplayRemoved]);
    }

    // <Transition name="fade" persisted><div key="a" v-show="show">content</div></Transition>. The
    // persisted flag simulates the compiler's transformTransition injection for a single v-show child.
    private static RenderComponent PersistedHost(Reference<object?> show, string? inlineDisplay = null)
        => new((_, _) => () =>
        {
            var divProperties = new VirtualNodeProperties();
            divProperties.Set("key", "a");
            if (inlineDisplay is not null)
            {
                divProperties.Set("style", new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["display"] = inlineDisplay,
                });
            }
            var slots = new ComponentSlots();
            slots["default"] = _ =>
            [
                Directives.WithDirectives(Element("div", divProperties, "content"), VShow.Instance, show.Value),
            ];
            var transitionProperties = Properties(("name", "fade"), ("persisted", true));
            return Component(Transition.Instance, transitionProperties, slots);
        });
}
