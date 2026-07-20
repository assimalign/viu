using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins the DOM <TransitionGroup> FLIP move through the deterministic in-memory adapter: after a
// reorder, the elements whose positions changed get the v-move class with an inverting transform, a
// single reflow is forced, and the class is removed on the move transition end. Upstream contract:
// @vue/runtime-dom components/TransitionGroup.ts (https://vuejs.org/guide/built-ins/transition-group.html).
public sealed class TransitionGroupTests : IDisposable
{
    private readonly TransitionTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Reorder_AppliesMoveClassAndInvertingTransform_OnlyToMovedItems()
    {
        var list = Reactive.Reference<string[]>(["1", "2", "3"]);
        _harness.Render(Group(list));

        var spans = _harness.FindElements("span");
        int one = spans[0], two = spans[1], three = spans[2];
        // No FLIP on the initial mount, no enter without appear.
        _harness.ClassLog.ShouldBeEmpty();

        // Old positions (read in the render) then new positions (read in onUpdated): items 1 and 3 move,
        // item 2 stays put.
        _harness.EnqueuePosition(one, 0, 0);
        _harness.EnqueuePosition(one, 0, 100);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(three, 0, 100);
        _harness.EnqueuePosition(three, 0, 0);

        list.Value = ["3", "1", "2"];
        _harness.RunUntilIdle();

        // The moved items get the inverting transform then the v-move class; the static item is untouched.
        // The inline transform is cleared afterwards so the move class animates back (upstream
        // applyTranslation + the moveClass loop); MoveLog is the recorded history of those writes.
        _harness.MoveLog.ShouldContain($"transform:{one}:0,-100");
        _harness.MoveLog.ShouldContain($"transform:{three}:0,100");
        _harness.MoveLog.ShouldNotContain($"transform:{two}:0,0");
        _harness.Classes(one).ShouldContain("v-move");
        _harness.Classes(three).ShouldContain("v-move");
        _harness.Classes(two).ShouldNotContain("v-move");
        // One read pass then one write pass: a single reflow separates them (upstream forceReflow).
        _harness.ReflowCount.ShouldBe(1);

        // The move class is removed when the transform transition ends.
        _harness.HasPendingMove(one).ShouldBeTrue();
        _harness.FireMoveEnd(one);
        _harness.Classes(one).ShouldNotContain("v-move");
    }

    [Fact]
    public void Reorder_WithoutCssTransform_SkipsTheFlipEntirely()
    {
        _harness.HasCssTransform = false;
        var list = Reactive.Reference<string[]>(["1", "2"]);
        _harness.Render(Group(list));
        var spans = _harness.FindElements("span");

        _harness.EnqueuePosition(spans[0], 0, 0);
        _harness.EnqueuePosition(spans[0], 0, 20);
        _harness.EnqueuePosition(spans[1], 0, 20);
        _harness.EnqueuePosition(spans[1], 0, 0);

        list.Value = ["2", "1"];
        _harness.RunUntilIdle();

        // hasCSSTransform gate is false -> no transforms, no move class, no reflow (upstream early return).
        _harness.MoveTransforms.ShouldBeEmpty();
        _harness.ReflowCount.ShouldBe(0);
        _harness.Classes(spans[0]).ShouldNotContain("v-move");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(9)]
    public void Reorder_BatchesEachPositionPass_IntoOneReadCrossing_RegardlessOfChildCount(int itemCount)
    {
        var initial = new string[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            initial[index] = $"{index + 1}";
        }
        var list = Reactive.Reference(initial);
        _harness.Render(Group(list));
        var spans = _harness.FindElements("span");

        // Old then new position per child — reversed layout so the ends move (the pre-patch snapshot dequeues
        // the old, the post-patch read the new).
        for (var index = 0; index < itemCount; index++)
        {
            _harness.EnqueuePosition(spans[index], 0, index * 10);
            _harness.EnqueuePosition(spans[index], 0, (itemCount - 1 - index) * 10);
        }

        var reversed = new string[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            reversed[index] = initial[itemCount - 1 - index];
        }
        list.Value = reversed;
        _harness.RunUntilIdle();

        // The batched-read acceptance criterion: exactly TWO read crossings for the whole reorder — one
        // pre-patch snapshot, one post-patch read — no matter how many children, and each crossing carries
        // the full child batch. Upstream reads getBoundingClientRect per child inside a same-process JS loop;
        // a handle platform batches the pass so N children cost one crossing, not N ([V01.01.04.07.03]).
        _harness.MeasurePositionsCallCount.ShouldBe(2);
        _harness.MeasuredBatchSizes.ShouldBe([itemCount, itemCount]);
        // Sanity: the FLIP actually ran (the first item moved to the far end).
        _harness.Classes(spans[0]).ShouldContain("v-move");
    }

    [Fact]
    public void InterruptedReorder_ForceFinishesTheInFlightMove_ThenConverges()
    {
        var list = Reactive.Reference<string[]>(["1", "2", "3"]);
        _harness.Render(Group(list));
        var spans = _harness.FindElements("span");
        int one = spans[0], two = spans[1], three = spans[2];

        // Two reorders' worth of positions per element (old, new, old, new): 2 stays put, 1 and 3 move both times.
        _harness.EnqueuePosition(one, 0, 0);
        _harness.EnqueuePosition(one, 0, 100);
        _harness.EnqueuePosition(one, 0, 100);
        _harness.EnqueuePosition(one, 0, 0);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(three, 0, 100);
        _harness.EnqueuePosition(three, 0, 0);
        _harness.EnqueuePosition(three, 0, 0);
        _harness.EnqueuePosition(three, 0, 100);

        // Reorder 1: 1 and 3 swap; both gain the move class and a pending move-end.
        list.Value = ["3", "2", "1"];
        _harness.RunUntilIdle();
        _harness.Classes(one).ShouldContain("v-move");
        _harness.HasPendingMove(one).ShouldBeTrue();

        // Reorder 2 BEFORE the first move ends: upstream callPendingCbs force-finishes the in-flight move
        // (the interrupted move class is removed) and the FLIP re-runs from the settled position — a fresh
        // pending move replaces the stale one rather than stacking a second transitionend listener.
        list.Value = ["1", "2", "3"];
        _harness.RunUntilIdle();
        _harness.Classes(one).ShouldContain("v-move");
        _harness.HasPendingMove(one).ShouldBeTrue();
        // The inverting transform is written then cleared within each FLIP, so nothing lingers inline.
        _harness.MoveTransforms.ShouldBeEmpty();

        // The final move end converges: no move class, no pending callback anywhere.
        _harness.FireMoveEnd(one);
        _harness.FireMoveEnd(three);
        _harness.Classes(one).ShouldNotContain("v-move");
        _harness.Classes(three).ShouldNotContain("v-move");
        _harness.HasPendingMove(one).ShouldBeFalse();
        _harness.HasPendingMove(three).ShouldBeFalse();
    }

    [Fact]
    public void CompletedReorder_LeavesNoResidue_OnceEveryMoveEnds()
    {
        var list = Reactive.Reference<string[]>(["1", "2", "3"]);
        _harness.Render(Group(list));
        var spans = _harness.FindElements("span");
        int one = spans[0], two = spans[1], three = spans[2];

        _harness.EnqueuePosition(one, 0, 0);
        _harness.EnqueuePosition(one, 0, 100);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(two, 0, 50);
        _harness.EnqueuePosition(three, 0, 100);
        _harness.EnqueuePosition(three, 0, 0);

        list.Value = ["3", "1", "2"];
        _harness.RunUntilIdle();

        // The FLIP wrote and then cleared the inverting transform for each moved child (upstream
        // applyTranslation then style.transform = ''), so no inline transform lingers even mid-animation.
        _harness.MoveTransforms.ShouldBeEmpty();
        _harness.HasPendingMove(one).ShouldBeTrue();
        _harness.HasPendingMove(three).ShouldBeTrue();
        // The static item never entered the move pipeline.
        _harness.Classes(two).ShouldNotContain("v-move");
        _harness.HasPendingMove(two).ShouldBeFalse();

        // Firing every move end removes the move class and drops the pending callback — the group is clean.
        _harness.FireMoveEnd(one);
        _harness.FireMoveEnd(three);
        _harness.Classes(one).ShouldNotContain("v-move");
        _harness.Classes(three).ShouldNotContain("v-move");
        _harness.HasPendingMove(one).ShouldBeFalse();
        _harness.HasPendingMove(three).ShouldBeFalse();
        _harness.MoveTransforms.ShouldBeEmpty();
    }

    // A component rendering <TransitionGroup tag="div"> over a keyed list of <span>s.
    private static RenderComponent Group(Reference<string[]> list)
        => new((_, _) => () =>
        {
            var slots = new ComponentSlots { Flag = SlotFlags.Dynamic };
            slots["default"] = _ =>
            {
                var items = list.Value;
                var children = new VirtualNode?[items.Length];
                for (var index = 0; index < items.Length; index++)
                {
                    children[index] = VirtualNodeFactory.Element(
                        "span",
                        VirtualNodeFactory.Properties(("key", items[index])),
                        items[index]);
                }
                return children;
            };
            return VirtualNodeFactory.Component(
                TransitionGroup.Instance,
                VirtualNodeFactory.Properties(("tag", "div")),
                slots);
        });
}
