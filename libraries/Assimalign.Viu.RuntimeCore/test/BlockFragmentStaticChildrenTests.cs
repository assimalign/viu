using System;
using System.Text;

using Shouldly;
using Xunit;

using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the traverseStaticChildren host-pointer carry-forward that processFragment's block branch owes
// a movable stable fragment ([V01.01.03.15.02]). A keyed v-for whose items are multi-root
// (STABLE_FRAGMENT) templates block-patches only each item's dynamic descendants, so the item's static
// (non-dynamic) children keep a null host pointer unless processFragment copies the old one forward; a
// reorder then MOVES an item and, with it, that static child — which reads the null pointer and throws
// without the fix. Upstream reference: @vue/runtime-core packages/runtime-core/src/renderer.ts
// processFragment (the STABLE_FRAGMENT && dynamicChildren branch calling traverseStaticChildren on a
// keyed #2080 / component-root #2134 fragment) + traverseStaticChildren —
// https://vuejs.org/guide/extras/rendering-mechanism.html.
public class BlockFragmentStaticChildrenTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public BlockFragmentStaticChildrenTests()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void KeyedReorder_MovesMultiRootStableFragmentItems_WithoutNullReference()
    {
        // The reported defect: before the fix, reordering NRE'd inside Move — the moved item's Fragment
        // arm walks its ArrayChildren and inserts each child by node.El, which was null for the static
        // <div> whose host pointer processFragment's block branch never carried forward.
        _renderer.Render(List("a", "b", "c"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe(Rendered("a", "b", "c"));
        _renderer.OperationLog.Reset();

        _renderer.Render(List("c", "b", "a"), _container);

        TestNodeSerializer.Serialize(_container).ShouldBe(Rendered("c", "b", "a"));
        // Reused, not remounted: a keyed reorder issues host moves only.
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    [Fact]
    public void KeyedReorder_KeepsTheBlockFastPath_VisitingOnlyDynamicChildren()
    {
        // The fix copies the static child's host pointer but must NOT walk the static subtree into a full
        // diff (traverseStaticChildren copies El references without a patch visit). Pin the patch-visit
        // count: the host <ul>, plus (the fragment + its one dynamic <span>) per item = 1 + 3*2 = 7. A
        // de-optimized full-children diff would additionally visit each item's static <div> (7 -> 10).
        _renderer.Render(List("a", "b", "c"), _container);
        _renderer.OperationLog.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;

        _renderer.Render(List("c", "b", "a"), _container);

        Renderer<TestNode>.PatchVisitCount.ShouldBe(7);
        // The reverse moves the two non-anchor items; each moved fragment relocates its start anchor, its
        // <div> and <span>, and its end anchor = 4 inserts, so 2 * 4 = 8 — never a remount.
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(8);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    [Fact]
    public void KeyedReorder_MovesBothDirections_SwappingMiddleThenRestoring()
    {
        // A middle swap moves one item forward and the other backward while the ends stay put, then the
        // inverse swap moves them back — exercising relocations in both directions over the fixed pointers.
        _renderer.Render(List("a", "b", "c", "d"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(List("a", "c", "b", "d"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe(Rendered("a", "c", "b", "d"));
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);

        _renderer.OperationLog.Reset();
        _renderer.Render(List("a", "b", "c", "d"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe(Rendered("a", "b", "c", "d"));
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    [Fact]
    public void UnmountAfterReorder_RemovesTheWholeFragmentRange_Cleanly()
    {
        // Reorder first (which relies on the fix), then drop the middle item: its fragment tears down over
        // the range the carried-forward pointers frame, leaving the survivors intact and un-remounted.
        _renderer.Render(List("a", "b", "c"), _container);
        _renderer.Render(List("c", "b", "a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(List("c", "a"), _container);

        TestNodeSerializer.Serialize(_container).ShouldBe(Rendered("c", "a"));
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        // The dropped item's whole owned range removes: start anchor + <div> + <span> + end anchor = 4.
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(4);
    }

    [Fact]
    public void NestedStableFragments_ReorderMovesTheNestedRange_WithoutNullReference()
    {
        // Each item's stable fragment nests another keyed stable-fragment block (a static <em> plus a
        // dynamic <span>). Moving the outer item recurses through Move into the nested fragment, so BOTH
        // the outer static <div> and the inner static <em> must have inherited the old host pointers.
        _renderer.Render(NestedList("a", "b"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe(NestedRendered("a", "b"));
        _renderer.OperationLog.Reset();

        _renderer.Render(NestedList("b", "a"), _container);

        TestNodeSerializer.Serialize(_container).ShouldBe(NestedRendered("b", "a"));
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    // One compiled v-for item: (openBlock(), createElementBlock(Fragment, { key }, [
    //   createElementVNode("div", null, key),          // static root (patchFlag 0, not collected)
    //   createElementVNode("span", null, key, 1 /*TEXT*/),  // dynamic root (collected)
    // ], 64 /* STABLE_FRAGMENT */)). Label == text == key keeps assertions legible and lets a moved
    // item keep its own (unchanged) text.
    private static VirtualNode Item(string key)
    {
        VirtualNodeFactory.OpenBlock();
        return VirtualNodeFactory.FragmentBlock(
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("div", key),
                VirtualNodeFactory.Element("span", null, key, PatchFlags.Text),
            },
            key,
            PatchFlags.StableFragment);
    }

    // The v-for's rendered list: a host <ul> whose children are the keyed stable-fragment items, in the
    // given key order (an element stands in for the v-for's parent; the reorder still runs the keyed diff).
    private static VirtualNode List(params string[] keys)
    {
        var children = new VirtualNode?[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            children[index] = Item(keys[index]);
        }
        return VirtualNodeFactory.Element("ul", null, children);
    }

    // A nested variant: each item's second root is itself a keyed stable-fragment block.
    private static VirtualNode NestedItem(string key)
    {
        VirtualNodeFactory.OpenBlock();
        VirtualNodeFactory.OpenBlock();
        var inner = VirtualNodeFactory.FragmentBlock(
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("em", key),
                VirtualNodeFactory.Element("span", null, key, PatchFlags.Text),
            },
            key + "-inner",
            PatchFlags.StableFragment);
        return VirtualNodeFactory.FragmentBlock(
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("div", key),
                inner,
            },
            key,
            PatchFlags.StableFragment);
    }

    private static VirtualNode NestedList(params string[] keys)
    {
        var children = new VirtualNode?[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            children[index] = NestedItem(keys[index]);
        }
        return VirtualNodeFactory.Element("ul", null, children);
    }

    private static string Rendered(params string[] keys)
    {
        var builder = new StringBuilder("<root><ul>");
        foreach (var key in keys)
        {
            builder.Append("<div>").Append(key).Append("</div><span>").Append(key).Append("</span>");
        }
        return builder.Append("</ul></root>").ToString();
    }

    private static string NestedRendered(params string[] keys)
    {
        var builder = new StringBuilder("<root><ul>");
        foreach (var key in keys)
        {
            builder.Append("<div>").Append(key).Append("</div><em>").Append(key)
                .Append("</em><span>").Append(key).Append("</span>");
        }
        return builder.Append("</ul></root>").ToString();
    }
}
