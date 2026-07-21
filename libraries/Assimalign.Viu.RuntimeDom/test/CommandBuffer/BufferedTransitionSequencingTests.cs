using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

using static Assimalign.Viu.VirtualNodeFactory;

namespace Assimalign.Viu.RuntimeDom.Tests;

// The heart of [V01.01.04.07.02]: a CSS <Transition> must actually animate under the batched interop
// mode. Batching would otherwise coalesce a whole flush's writes into one style recalc, applying the
// *-enter-from and *-enter-to classes together and eliminating the browser's transition trigger. This
// battery drives the REAL renderer + REAL DOM <Transition> through the BUFFERED node-ops over the
// in-memory command-buffer applier and proves the browser-observable sequence survives: from/active in
// one frame, the to-swap in a distinct later frame, and the leave reflow barrier landing between the
// from- and active-class writes inside a single one-crossing frame. Upstream contract: @vue/runtime-dom
// components/Transition.ts forceReflow + double-requestAnimationFrame nextFrame
// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/components/Transition.ts).
// Browser-annotated like the other command-buffer tests (nothing crosses a real interop boundary — the
// applier is the in-memory CommandBufferDecoder and the timing seam is a recording fake).
[SupportedOSPlatform("browser")]
public sealed class BufferedTransitionSequencingTests
{
    [Fact]
    public void Enter_AppliesFromAndActiveInOneFrame_ThenSwapsToTheToClassInADistinctFrame()
    {
        using var world = new BufferedTransitionWorld();
        var show = Reactive.Reference(false);
        world.Render(Host(show, ("name", "fade")));

        // Toggle in: the mount flush commits create + enter-from + enter-active together (the element
        // mounts already carrying the from/active classes), but NOT the to-class — one boundary crossing.
        show.Value = true;
        world.RunUntilIdle();
        var enterFrame = world.FirstTransitionFrameContaining("add:fade-enter-from");
        enterFrame.ShouldBe(["add:fade-enter-from", "add:fade-enter-active"]);
        enterFrame.ShouldNotContain("add:fade-enter-to"); // not coalesced into the mount frame
        var div = world.Dom.FindFirstElement("div");
        world.Dom.TransitionClasses(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);

        // Next frame (the real double-rAF continuation): the from -> to swap commits in its OWN frame,
        // two frames after the from/active frame, so the browser sees a real class change and transitions.
        world.AdvanceFrame();
        var swapFrame = world.FirstTransitionFrameContaining("add:fade-enter-to");
        swapFrame.ShouldBe(["remove:fade-enter-from", "add:fade-enter-to"]);
        world.FrameIndexOf(swapFrame).ShouldBeGreaterThan(world.FrameIndexOf(enterFrame)); // distinct frames
        world.Dom.TransitionClasses(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);

        // The transition end removes the to+active classes (finishEnter), also committed as its own frame.
        world.FireTransitionEnd(div);
        world.Dom.TransitionClasses(div).ShouldBeEmpty();
    }

    [Fact]
    public void Leave_LandsTheReflowBarrierBetweenFromAndActive_WithinOneFrame_ThenSwapsInADistinctFrame()
    {
        using var world = new BufferedTransitionWorld();
        var show = Reactive.Reference(true);
        world.Render(Host(show, ("name", "fade")));
        var div = world.Dom.FindFirstElement("div");
        world.Dom.ReflowCount.ShouldBe(0); // no appear -> the initial mount runs no choreography or reflow

        // Toggle out: within a SINGLE flush the buffered adaptor commits leave-from, then a real reflow
        // (the ForceReflow barrier op — document.body.offsetHeight), then leave-active, in that order —
        // upstream #2593. The barrier does not split the flush; it is one op inside the one crossing.
        show.Value = false;
        world.RunUntilIdle();
        var leaveFrame = world.FirstTransitionFrameContaining("add:fade-leave-from");
        leaveFrame.ShouldBe(["add:fade-leave-from", "reflow", "add:fade-leave-active"]);
        world.Dom.ReflowCount.ShouldBe(1);
        world.Dom.TransitionClasses(div).ShouldBe(["fade-leave-from", "fade-leave-active"], ignoreOrder: true);
        world.Dom.IsMounted(div).ShouldBeTrue(); // removal deferred behind the leave animation

        // Next frame: the from -> to swap is a distinct later frame (never coalesced with from/active).
        world.AdvanceFrame();
        var swapFrame = world.FirstTransitionFrameContaining("add:fade-leave-to");
        swapFrame.ShouldBe(["remove:fade-leave-from", "add:fade-leave-to"]);
        world.FrameIndexOf(swapFrame).ShouldBeGreaterThan(world.FrameIndexOf(leaveFrame));
        world.Dom.TransitionClasses(div).ShouldBe(["fade-leave-active", "fade-leave-to"], ignoreOrder: true);

        // The transition end removes the leave classes and finally host-removes the element.
        world.FireTransitionEnd(div);
        world.Dom.IsMounted(div).ShouldBeFalse();
    }

    [Fact]
    public void ClassAndReflowOps_NeverReachTheDirectBridge_InBufferedMode()
    {
        using var world = new BufferedTransitionWorld();
        var show = Reactive.Reference(false);
        world.Render(Host(show, ("name", "fade")));

        show.Value = true;
        world.RunUntilIdle();
        world.AdvanceFrame();
        world.FireTransitionEnd(world.Dom.FindFirstElement("div"));

        // Every class write and every reflow went through the command buffer (opcodes 21/22/23), not the
        // direct bridge delegates — otherwise a buffered create's handle would be unknown to the applier.
        world.DirectClassWrites.ShouldBeEmpty();
        world.DirectReflowCount.ShouldBe(0);
    }

    [Fact]
    public void PersistedShow_AppliesEnterFromAndActiveInOneFrame_ThenSwapsToInADistinctFrame_WithoutUnmounting()
    {
        using var world = new BufferedTransitionWorld();
        var show = Reactive.Reference(false);
        world.Render(PersistedHost(show, ("name", "fade")));
        var div = world.Dom.FindFirstElement("div");
        world.Dom.IsMounted(div).ShouldBeTrue(); // v-show keeps the element mounted even while hidden

        // Toggle show: the persisted v-show path drives the enter, so the from + active classes commit
        // together in ONE buffered frame (one crossing), but NOT the to-class — same barrier as a v-if enter.
        show.Value = true;
        world.RunUntilIdle();
        var enterFrame = world.FirstTransitionFrameContaining("add:fade-enter-from");
        enterFrame.ShouldBe(["add:fade-enter-from", "add:fade-enter-active"]);
        enterFrame.ShouldNotContain("add:fade-enter-to");
        world.Dom.TransitionClasses(div).ShouldBe(["fade-enter-from", "fade-enter-active"], ignoreOrder: true);

        // Next frame: the from -> to swap commits in its OWN distinct frame (never coalesced).
        world.AdvanceFrame();
        var swapFrame = world.FirstTransitionFrameContaining("add:fade-enter-to");
        swapFrame.ShouldBe(["remove:fade-enter-from", "add:fade-enter-to"]);
        world.FrameIndexOf(swapFrame).ShouldBeGreaterThan(world.FrameIndexOf(enterFrame));
        world.Dom.TransitionClasses(div).ShouldBe(["fade-enter-active", "fade-enter-to"], ignoreOrder: true);

        // The transition end removes the enter classes; the element was never unmounted.
        world.FireTransitionEnd(div);
        world.Dom.TransitionClasses(div).ShouldBeEmpty();
        world.Dom.IsMounted(div).ShouldBeTrue();
    }

    [Fact]
    public void PersistedHide_LandsTheReflowBarrierBetweenFromAndActive_ThenHidesAfterTheLeave_WithoutUnmounting()
    {
        using var world = new BufferedTransitionWorld();
        var show = Reactive.Reference(true);
        world.Render(PersistedHost(show, ("name", "fade")));
        var div = world.Dom.FindFirstElement("div");
        world.Dom.ReflowCount.ShouldBe(0); // no appear -> the initial mount runs no choreography or reflow

        // Toggle hide: within a SINGLE flush the buffered adaptor commits leave-from, a real reflow barrier
        // (document.body.offsetHeight), then leave-active, in that order (upstream #2593) — one crossing. The
        // element stays MOUNTED and visible; the removal path is never taken (v-show persists it).
        show.Value = false;
        world.RunUntilIdle();
        var leaveFrame = world.FirstTransitionFrameContaining("add:fade-leave-from");
        leaveFrame.ShouldBe(["add:fade-leave-from", "reflow", "add:fade-leave-active"]);
        world.Dom.ReflowCount.ShouldBe(1);
        world.Dom.TransitionClasses(div).ShouldBe(["fade-leave-from", "fade-leave-active"], ignoreOrder: true);
        world.Dom.IsMounted(div).ShouldBeTrue();

        // Next frame: the from -> to swap is a distinct later frame (never coalesced with from/active).
        world.AdvanceFrame();
        var swapFrame = world.FirstTransitionFrameContaining("add:fade-leave-to");
        swapFrame.ShouldBe(["remove:fade-leave-from", "add:fade-leave-to"]);
        world.FrameIndexOf(swapFrame).ShouldBeGreaterThan(world.FrameIndexOf(leaveFrame));
        world.Dom.TransitionClasses(div).ShouldBe(["fade-leave-active", "fade-leave-to"], ignoreOrder: true);

        // The transition end removes the leave classes and applies display:none — but the element stays
        // MOUNTED (the v-show leave hides in place; it never host-removes like the v-if leave).
        world.FireTransitionEnd(div);
        world.Dom.TransitionClasses(div).ShouldBeEmpty();
        world.Dom.IsMounted(div).ShouldBeTrue();
        world.Dom.Serialize(div).ShouldContain("style.display=\"none\"");
    }

    // A component rendering <Transition {props}> around a v-if div keyed "a" (mirrors TransitionTests.Host).
    private static RenderComponent Host(Reference<bool> show, params (string Name, object? Value)[] transitionProperties)
        => new((_, _) => () =>
        {
            var slots = new ComponentSlots();
            slots["default"] = _ => show.Value
                ? [Element("div", Properties(("key", "a")), "A")]
                : [Comment()];
            return Component(Transition.Instance, Properties(transitionProperties), slots);
        });

    // A component rendering <Transition {props} persisted> around a v-show div keyed "a" — the persisted
    // path (#161). The persisted flag stands in for the compiler's transformTransition injection for a
    // single v-show child, so the renderer skips its mount/remove enter/leave and v-show drives them.
    private static RenderComponent PersistedHost(Reference<bool> show, params (string Name, object? Value)[] transitionProperties)
        => new((_, _) => () =>
        {
            var slots = new ComponentSlots();
            slots["default"] = _ =>
            [
                Directives.WithDirectives(Element("div", Properties(("key", "a")), "A"), VShow.Instance, show.Value),
            ];
            var properties = Properties(transitionProperties);
            properties.Set("persisted", true);
            return Component(Transition.Instance, properties, slots);
        });

    // A DOM-free buffered world: the real renderer over BufferedBrowserNodeOperations, an in-memory
    // command-buffer applier that records each frame's transition-op sequence, and a recording "direct"
    // DomTransitionOperations standing in for the rAF/end-detection bridge (frames and end events are
    // advanced on demand). The buffered adaptor wraps this recording instance exactly as production wraps
    // the browser-backed one.
    private sealed class BufferedTransitionWorld : IDisposable
    {
        private readonly InMemoryHandleDom _dom = new();
        private readonly BufferedBrowserNodeOperations _buffered;
        private readonly Renderer<int> _renderer;
        private readonly TestSchedulerPump _pump;
        private readonly DomTransitionOperations? _previousTransitionOperations;
        private readonly int _container;
        private readonly List<Action> _nextFrameQueue = [];
        private readonly Dictionary<int, Action> _endResolvers = [];

        public BufferedTransitionWorld()
        {
            Scheduler.Reset();
            _pump = TestSchedulerPump.Install();
            _container = _dom.CreateElement("root", null);

            // Force BrowserNodeOperations' static constructor (which installs the bridge-backed transition
            // ops) to run BEFORE we install the recording fake, so buffered Activate captures the fake
            // rather than the bridge — the ctor would otherwise clobber Current mid-Activate.
            _ = BrowserNodeOperations.OverrideDispatcher;
            _previousTransitionOperations = DomTransitionOperations.Current;
            DomTransitionOperations.Current = BuildRecordingDirectOperations();

            _buffered = new BufferedBrowserNodeOperations(
                (frame, length) =>
                {
                    var before = _dom.TransitionLog.Count;
                    var released = CommandBufferDecoder.Apply(frame, length, _dom);
                    var produced = _dom.TransitionLog.Count - before;
                    if (produced > 0)
                    {
                        TransitionFrames.Add(_dom.TransitionLog.GetRange(before, produced));
                    }
                    // Sanity: the frame is versioned/well-formed (guards a silent header drift).
                    frame[0].ShouldBe(DomCommandBuffer.Magic);
                    frame[1].ShouldBe(DomCommandBuffer.Version);
                    return released;
                },
                static _ => 0,
                _dom.ParentNode,
                _dom.NextSibling,
                _dom.InsertStaticContent);
            _buffered.Activate();
            _buffered.ObserveForeignHandle(_container);
            _renderer = RendererFactory.CreateRenderer(_buffered.Create());
        }

        /// <summary>The per-frame transition-op sequences ("add:x"/"remove:x"/"reflow"), one list per apply that produced any.</summary>
        public List<List<string>> TransitionFrames { get; } = [];

        /// <summary>Class writes that (incorrectly) reached the direct bridge instead of the buffer — must stay empty.</summary>
        public List<string> DirectClassWrites { get; } = [];

        /// <summary>Reflows that (incorrectly) reached the direct bridge instead of the barrier op — must stay zero.</summary>
        public int DirectReflowCount { get; private set; }

        /// <summary>The in-memory DOM the applier replays each frame onto.</summary>
        public InMemoryHandleDom Dom => _dom;

        public void Render(IComponentDefinition component)
        {
            _renderer.Render(Component(component), _container);
            _pump.RunUntilIdle();
        }

        public void RunUntilIdle() => _pump.RunUntilIdle();

        /// <summary>Runs the queued double-rAF continuations (the deterministic stand-in for the browser's next frame).</summary>
        public void AdvanceFrame()
        {
            var pending = new List<Action>(_nextFrameQueue);
            _nextFrameQueue.Clear();
            foreach (var callback in pending)
            {
                callback();
            }
        }

        /// <summary>Fires the transition-end for an element, completing its in-flight enter/leave.</summary>
        public void FireTransitionEnd(int element)
        {
            if (_endResolvers.Remove(element, out var resolve))
            {
                resolve();
            }
        }

        /// <summary>The first recorded frame whose op sequence contains <paramref name="op"/>.</summary>
        public IReadOnlyList<string> FirstTransitionFrameContaining(string op)
            => TransitionFrames.Find(frame => frame.Contains(op))
               ?? throw new InvalidOperationException($"No applied frame contained '{op}'. Frames: "
                   + string.Join(" | ", TransitionFrames.ConvertAll(frame => string.Join(",", frame))));

        /// <summary>The apply/frame index of a recorded frame (its position in the flush order).</summary>
        public int FrameIndexOf(IReadOnlyList<string> frame) => TransitionFrames.FindIndex(candidate => ReferenceEquals(candidate, frame));

        public void Dispose()
        {
            _buffered.Deactivate();
            DomTransitionOperations.Current = _previousTransitionOperations;
            _pump.Dispose();
            Scheduler.Reset();
        }

        private DomTransitionOperations BuildRecordingDirectOperations() => new()
        {
            // The buffered wrapper routes class writes and the reflow through the command buffer, so these
            // direct delegates must never fire in buffered mode — recording them proves that contract.
            AddTransitionClass = (_, cssClass) => DirectClassWrites.Add("add:" + cssClass),
            RemoveTransitionClass = (_, cssClass) => DirectClassWrites.Add("remove:" + cssClass),
            ForceReflow = () => DirectReflowCount++,
            // The rAF scheduling and end-detection stay direct (the buffered wrapper flushes around them).
            NextFrame = _nextFrameQueue.Add,
            WhenTransitionEnds = (element, _, _, resolve) => _endResolvers[element] = resolve,
            // FLIP ops are unused by <Transition>; stub them (their own batched coverage is in the
            // TransitionGroup adapter and command-buffer tests, [V01.01.04.07.03]).
            MeasurePositions = handles => new TransitionRectangle[handles.Length],
            SetMoveTransform = (_, _, _) => { },
            ClearMoveStyles = _ => { },
            HasCssTransform = (_, _, _) => false,
            WhenMoveEnds = (_, resolve) => resolve(),
        };
    }
}
