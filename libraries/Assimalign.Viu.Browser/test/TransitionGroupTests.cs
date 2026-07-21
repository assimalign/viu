using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Browser.Tests;

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

    // --- attribute fallthrough ([V01.01.04.07.04]) ----------------------------------------------
    // Upstream TransitionGroup renders createVNode(tag, null, children) and does nothing special for
    // attrs: the standard single-root fallthrough (packages/runtime-core/src/componentAttrs.ts, applied
    // by renderComponentRoot) lands class/style/arbitrary attributes on that tag element, while the
    // declared props (tag/moveClass + the transition props) are consumed. In fragment mode there is no
    // element root, so the attrs have no target and are dropped with no warning.

    [Fact]
    public void Tag_FallsThroughClassStyleAndArbitraryAttributes_OntoTheWrapperElement()
    {
        _harness.Render(GroupWith(
            () => ["1", "2"],
            () => VirtualNodeFactory.Properties(
                ("tag", "ul"),
                ("name", "fade"),
                ("class", "list-wrapper"),
                ("style", "color:red"),
                ("id", "grp"),
                ("data-role", "list"))));

        var wrapper = _harness.FindElement("ul");
        // class/style/arbitrary attributes fall through onto the rendered tag element.
        _harness.BoundProperty(wrapper, "class").ShouldBe("list-wrapper");
        _harness.BoundProperty(wrapper, "style").ShouldBe("color:red");
        _harness.BoundProperty(wrapper, "id").ShouldBe("grp");
        _harness.BoundProperty(wrapper, "data-role").ShouldBe("list");
        // Declared props are consumed — tag/name never leak onto the wrapper as literal attributes.
        _harness.BoundProperty(wrapper, "tag").ShouldBeNull();
        _harness.BoundProperty(wrapper, "name").ShouldBeNull();
    }

    [Fact]
    public void Fragment_NoTag_HasNoFallthroughTarget_AndEmitsNoWarning()
    {
        var warnings = new List<string>();
        var previousSink = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = warnings.Add;
        try
        {
            _harness.Render(GroupWith(
                () => ["1", "2"],
                () => VirtualNodeFactory.Properties(("class", "orphan"), ("id", "no-target"))));

            // No tag -> a fragment root -> no single element to inherit onto: the only elements are the
            // children, and the group's class/id land on none of them.
            foreach (var span in _harness.FindElements("span"))
            {
                _harness.BoundProperty(span, "class").ShouldBeNull();
                _harness.BoundProperty(span, "id").ShouldBeNull();
            }
            // The target-less fallthrough is silent — no "extraneous attributes" warning (AC).
            warnings.ShouldBeEmpty();
        }
        finally
        {
            RuntimeWarnings.Sink = previousSink;
        }
    }

    [Fact]
    public void FallthroughClass_LandsOnWrapper_WithoutContaminatingChildMoveClasses()
    {
        var list = Reactive.Reference<string[]>(["1", "2", "3"]);
        _harness.Render(GroupWith(
            () => list.Value,
            () => VirtualNodeFactory.Properties(("tag", "ul"), ("class", "list-wrapper"))));

        var wrapper = _harness.FindElement("ul");
        var spans = _harness.FindElements("span");
        int one = spans[0], three = spans[2];

        // The wrapper carries the fallthrough class (element-prop channel) and no choreography class.
        _harness.BoundProperty(wrapper, "class").ShouldBe("list-wrapper");
        _harness.Classes(wrapper).ShouldNotContain("v-move");

        // Reorder so items 1 and 3 change pixel position (item 2 stays) and gain the move class.
        _harness.EnqueuePosition(one, 0, 0);
        _harness.EnqueuePosition(one, 0, 100);
        _harness.EnqueuePosition(spans[1], 0, 50);
        _harness.EnqueuePosition(spans[1], 0, 50);
        _harness.EnqueuePosition(three, 0, 100);
        _harness.EnqueuePosition(three, 0, 0);
        list.Value = ["3", "1", "2"];
        _harness.RunUntilIdle();

        // Children get the v-move choreography class (transition-class channel); the wrapper never does,
        // and its fallthrough class is untouched — the two channels never cross-contaminate.
        _harness.Classes(one).ShouldContain("v-move");
        _harness.Classes(three).ShouldContain("v-move");
        _harness.Classes(wrapper).ShouldNotContain("v-move");
        _harness.BoundProperty(wrapper, "class").ShouldBe("list-wrapper");
        // The wrapper's fallthrough class never leaks onto a child element.
        _harness.BoundProperty(one, "class").ShouldBeNull();
        _harness.BoundProperty(three, "class").ShouldBeNull();
    }

    [Fact]
    public void FallthroughAttributeUpdate_PatchesTheWrapper_OnReRender()
    {
        var cssClass = Reactive.Reference("wrapper-a");
        _harness.Render(GroupWith(
            () => ["1", "2"],
            () => VirtualNodeFactory.Properties(("tag", "ul"), ("class", cssClass.Value))));

        var wrapper = _harness.FindElement("ul");
        _harness.BoundProperty(wrapper, "class").ShouldBe("wrapper-a");

        // A reactive change to the fallthrough attr re-renders the group; renderComponentRoot re-merges
        // the live attrs and the patch updates the same wrapper element in place.
        cssClass.Value = "wrapper-b";
        _harness.RunUntilIdle();

        _harness.FindElement("ul").ShouldBe(wrapper);
        _harness.BoundProperty(wrapper, "class").ShouldBe("wrapper-b");
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

    // A component rendering a <TransitionGroup> (props from the factory, re-read each render so reactive
    // values retrigger) over a keyed list of <span>s. Props with no "tag" entry render the fragment form;
    // props beyond the declared set (class/style/arbitrary) exercise attribute fallthrough.
    private static RenderComponent GroupWith(Func<string[]> items, Func<VirtualNodeProperties> properties)
        => new((_, _) => () =>
        {
            var slots = new ComponentSlots { Flag = SlotFlags.Dynamic };
            slots["default"] = _ =>
            {
                var current = items();
                var children = new VirtualNode?[current.Length];
                for (var index = 0; index < current.Length; index++)
                {
                    children[index] = VirtualNodeFactory.Element(
                        "span",
                        VirtualNodeFactory.Properties(("key", current[index])),
                        current[index]);
                }
                return children;
            };
            return VirtualNodeFactory.Component(TransitionGroup.Instance, properties(), slots);
        });
}
