using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the platform-agnostic transition state machine (BaseTransition) against the in-memory renderer,
// mirroring upstream runtime-core/__tests__/components/BaseTransition.spec.ts: mock enter/leave hooks
// whose run counts and ordering pin the choreography (no CSS, no browser). The mode/appear/cancel
// behavior is the upstream contract — packages/runtime-core/src/components/BaseTransition.ts.
public sealed class BaseTransitionTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public BaseTransitionTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Mount_WithoutAppear_DoesNotRunEnterHooks()
    {
        var recorder = new TransitionRecorder();
        var toggle = Reactive.Reference(true);
        Mount(recorder.Build(), toggle);

        // Upstream: an initial mount without `appear` skips all enter hooks (isMounted is still false).
        recorder.Calls.ShouldBeEmpty();
        Serialize().ShouldBe("<root><div>A</div></root>");
    }

    [Fact]
    public void Mount_WithAppear_RunsAppearHooksOnce()
    {
        var recorder = new TransitionRecorder();
        var toggle = Reactive.Reference(true);
        Mount(recorder.Build(appear: true), toggle);

        // Appear remaps enter -> appear hooks on the first mount (upstream appear handling).
        recorder.Calls.ShouldBe(["beforeAppear", "appear"]);
        recorder.EnterRuns.ShouldBe(1);
        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(["beforeAppear", "appear", "afterAppear"]);
    }

    [Fact]
    public void Leave_RunsLeaveHooksOnce_AndDefersRemovalUntilDone()
    {
        var recorder = new TransitionRecorder();
        var toggle = Reactive.Reference(true);
        Mount(recorder.Build(), toggle);

        toggle.Value = false;
        _pump.RunUntilIdle();

        // The leaving element stays in the DOM (alongside the v-if comment placeholder) until the leave
        // completes (upstream: hostRemove deferred behind the leave).
        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        recorder.LeaveRuns.ShouldBe(1);
        Serialize().ShouldBe("<root><div>A</div><!----></root>");

        recorder.CompleteLeave();
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
        Serialize().ShouldBe("<root><!----></root>");
    }

    [Fact]
    public void EnterThenLeaveCycle_RunsEachPhaseWithPinnedCounts()
    {
        var recorder = new TransitionRecorder();
        var toggle = Reactive.Reference(true);
        Mount(recorder.Build(), toggle);

        // Leave.
        toggle.Value = false;
        _pump.RunUntilIdle();
        recorder.CompleteLeave();

        // Re-enter: isMounted is now true, so enter hooks run (no appear remap).
        toggle.Value = true;
        _pump.RunUntilIdle();
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave", "beforeEnter", "enter"]);
        recorder.EnterRuns.ShouldBe(1);
        recorder.CompleteEnter();

        recorder.Calls[^1].ShouldBe("afterEnter");
        recorder.AfterEnterRuns.ShouldBe(1);
        Serialize().ShouldBe("<root><div>A</div></root>");
    }

    [Fact]
    public void LeaveCancelledByReEnter_FiresEnterCancelled_NotAfterEnter()
    {
        var recorder = new TransitionRecorder();
        var toggle = Reactive.Reference(true);
        Mount(recorder.Build(), toggle);

        // Full leave/re-enter to get the element into an entering state, then interrupt with a leave.
        toggle.Value = false;
        _pump.RunUntilIdle();
        recorder.CompleteLeave();
        toggle.Value = true;
        _pump.RunUntilIdle();
        recorder.EnterRuns.ShouldBe(1); // entering, not yet completed
        recorder.Reset();

        // Interrupt the in-flight enter with a leave: the enter is cancelled (upstream el[enterCbKey](true)).
        toggle.Value = false;
        _pump.RunUntilIdle();

        recorder.Calls.ShouldBe(["enterCancelled", "beforeLeave", "leave"]);
        recorder.EnterCancelledRuns.ShouldBe(1);
        recorder.AfterEnterRuns.ShouldBe(0);
    }

    [Fact]
    public void OutInMode_LeavesOldFullyBeforeMountingNew()
    {
        var recorder = new TransitionRecorder();
        // A key selector: "a" renders <div>A</div>, "b" renders <span>B</span> (different vnode type).
        var which = Reactive.Reference("a");
        MountKeyed(recorder.Build(mode: "out-in"), which);

        // Swap to a different child: out-in defers the incoming mount until the outgoing leave finishes.
        which.Value = "b";
        _pump.RunUntilIdle();

        // Only the leave of the old child has begun; the new child is not mounted yet — out-in renders an
        // empty (comment) placeholder while the old child leaves in place.
        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        Serialize().ShouldBe("<root><div>A</div><!----></root>");

        // Completing the leave removes the old child and, via afterLeave, re-renders to mount+enter the new.
        recorder.CompleteLeave();
        _pump.RunUntilIdle();
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave", "beforeEnter", "enter"]);
        Serialize().ShouldBe("<root><span>B</span></root>");

        recorder.CompleteEnter();
        recorder.Calls[^1].ShouldBe("afterEnter");
    }

    [Fact]
    public void InOutMode_EntersNewBeforeLeavingOld()
    {
        var recorder = new TransitionRecorder();
        var which = Reactive.Reference("a");
        MountKeyed(recorder.Build(mode: "in-out"), which);

        which.Value = "b";
        _pump.RunUntilIdle();

        // in-out: the incoming child enters first; the outgoing leave is deferred (delayLeave), so no
        // leave hook has fired yet and both elements are present.
        recorder.Calls.ShouldBe(["beforeEnter", "enter"]);
        recorder.LeaveRuns.ShouldBe(0);
        Serialize().ShouldBe("<root><div>A</div><span>B</span></root>");

        // Completing the enter fires the deferred leave of the old child.
        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(["beforeEnter", "enter", "afterEnter", "beforeLeave", "leave"]);
        recorder.LeaveRuns.ShouldBe(1);

        recorder.CompleteLeave();
        Serialize().ShouldBe("<root><span>B</span></root>");
    }

    // --- harness ------------------------------------------------------------------------------

    private void Mount(BaseTransitionProperties properties, Reference<bool> toggle)
    {
        var slots = new ComponentSlots();
        slots["default"] = _ => toggle.Value
            ? [VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("key", "a")), "A")]
            : [VirtualNodeFactory.Comment()];
        var bag = VirtualNodeFactory.Properties((BaseTransition.PropertiesKey, properties));
        var host = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Component(BaseTransition.Instance, bag, slots),
        };
        _renderer.Render(VirtualNodeFactory.Component(host), _container);
        _pump.RunUntilIdle();
    }

    private void MountKeyed(BaseTransitionProperties properties, Reference<string> which)
    {
        var slots = new ComponentSlots { Flag = Shared.SlotFlags.Dynamic };
        slots["default"] = _ => which.Value == "a"
            ? [VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("key", "a")), "A")]
            : [VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("key", "b")), "B")];
        var bag = VirtualNodeFactory.Properties((BaseTransition.PropertiesKey, properties));
        var host = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Component(BaseTransition.Instance, bag, slots),
        };
        _renderer.Render(VirtualNodeFactory.Component(host), _container);
        _pump.RunUntilIdle();
    }

    private string Serialize() => TestNodeSerializer.Serialize(_container);

    // Records every hook call in order, pins per-phase run counts, and captures the pending enter/leave
    // done callbacks so a test can complete a transition deterministically (the mock stand-in for a real
    // transitionend). One "current" done per phase suffices because a test completes phases sequentially.
    private sealed class TransitionRecorder
    {
        private Action? _enterDone;
        private Action? _leaveDone;

        public List<string> Calls { get; } = [];

        public int EnterRuns { get; private set; }

        public int AfterEnterRuns { get; private set; }

        public int EnterCancelledRuns { get; private set; }

        public int LeaveRuns { get; private set; }

        public void Reset() => Calls.Clear();

        public void CompleteEnter()
        {
            var done = _enterDone;
            _enterDone = null;
            done?.Invoke();
        }

        public void CompleteLeave()
        {
            var done = _leaveDone;
            _leaveDone = null;
            done?.Invoke();
        }

        public BaseTransitionProperties Build(string? mode = null, bool appear = false)
            => new()
            {
                Mode = mode,
                Appear = appear,
                OnBeforeEnter = _ => Calls.Add("beforeEnter"),
                OnEnter = (_, done) => { Calls.Add("enter"); EnterRuns++; _enterDone = done; },
                OnAfterEnter = _ => { Calls.Add("afterEnter"); AfterEnterRuns++; },
                OnEnterCancelled = _ => { Calls.Add("enterCancelled"); EnterCancelledRuns++; },
                OnBeforeLeave = _ => Calls.Add("beforeLeave"),
                OnLeave = (_, done) => { Calls.Add("leave"); LeaveRuns++; _leaveDone = done; },
                OnAfterLeave = _ => Calls.Add("afterLeave"),
                OnLeaveCancelled = _ => Calls.Add("leaveCancelled"),
                OnBeforeAppear = _ => Calls.Add("beforeAppear"),
                OnAppear = (_, done) => { Calls.Add("appear"); EnterRuns++; _enterDone = done; },
                OnAfterAppear = _ => Calls.Add("afterAppear"),
                OnAppearCancelled = _ => Calls.Add("appearCancelled"),
            };
    }
}
