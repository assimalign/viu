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
