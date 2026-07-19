using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins keyed children reconciliation against @vue/runtime-core's patchKeyedChildren + getSequence
// (renderer.ts) — https://vuejs.org/guide/essentials/list.html#maintaining-state-with-key. Op
// counts are asserted for the canonical scenarios: a reorder must issue the minimal host moves
// (each move is one insert interop call), never N removes + N inserts.
public class KeyedChildrenDiffTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public KeyedChildrenDiffTests()
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
    public void Reverse_ReordersWithMinimalMoves_NotRemounts()
    {
        _renderer.Render(KeyedList("1", "2", "3", "4", "5"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("5", "4", "3", "2", "1"), _container);

        RenderedKeys(_container).ShouldBe(["5", "4", "3", "2", "1"]);
        // Minimal: one node stays (the LIS), the other N-1 move. Never remounted.
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(4);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
    }

    [Fact]
    public void SwapTwoEnds_MovesOnlyTheTwoEnds()
    {
        _renderer.Render(KeyedList("a", "b", "c", "d"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("d", "b", "c", "a"), _container);

        RenderedKeys(_container).ShouldBe(["d", "b", "c", "a"]);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(2); // b, c form the stable run
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
    }

    [Fact]
    public void Rotate_MovesOnlyTheRotatedNode()
    {
        _renderer.Render(KeyedList("a", "b", "c", "d"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("b", "c", "d", "a"), _container);

        RenderedKeys(_container).ShouldBe(["b", "c", "d", "a"]);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(1); // b, c, d stay; only a moves
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
    }

    [Fact]
    public void ShuffleWithAddAndRemove_ConvergesWithMinimalOps()
    {
        _renderer.Render(KeyedList("a", "b", "c", "d"), _container);
        _renderer.OperationLog.Reset();

        // Remove c, add e, and reorder: only e is created, only c is removed, and the reused nodes
        // move minimally (a stays as the stable run; b and d move).
        _renderer.Render(KeyedList("d", "b", "e", "a"), _container);

        RenderedKeys(_container).ShouldBe(["d", "b", "e", "a"]);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(3); // 1 mount + 2 moves
    }

    [Fact]
    public void PrependAndAppend_MountNewNodesWithoutMovingExisting()
    {
        _renderer.Render(KeyedList("b", "c"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("a", "b", "c", "d"), _container);

        RenderedKeys(_container).ShouldBe(["a", "b", "c", "d"]);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(2); // only a and d
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    [Fact]
    public void RemoveFromMiddle_UnmountsOnlyTheRemoved_WithNoMoves()
    {
        _renderer.Render(KeyedList("a", "b", "c", "d"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("a", "c", "d"), _container);

        RenderedKeys(_container).ShouldBe(["a", "c", "d"]);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(0); // head/tail sync, no reorder
    }

    [Fact]
    public void UnchangedKeyedList_ProducesNoStructuralOps()
    {
        _renderer.Render(KeyedList("a", "b", "c"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(KeyedList("a", "b", "c"), _container);

        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
    }

    [Fact]
    public void KeyedComponents_Reverse_MoveSubtrees_NotRemount()
    {
        // Reused component definitions keyed by name so the same-type check matches across renders.
        var definitions = new Dictionary<string, IComponentDefinition>(StringComparer.Ordinal);
        IComponentDefinition Definition(string key)
        {
            if (!definitions.TryGetValue(key, out var definition))
            {
                definition = new TestComponent
                {
                    SetupFunction = (_, _) => () => VirtualNodeFactory.Element("span", key),
                };
                definitions[key] = definition;
            }
            return definition;
        }
        VirtualNode ComponentList(params string[] keys)
        {
            var children = new VirtualNode?[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                children[index] = VirtualNodeFactory.Component(
                    Definition(keys[index]), VirtualNodeFactory.Properties(("key", keys[index])));
            }
            return VirtualNodeFactory.Element("div", null, children);
        }

        _renderer.Render(ComponentList("a", "b", "c"), _container);
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div><span>a</span><span>b</span><span>c</span></div></root>");
        _renderer.OperationLog.Reset();

        _renderer.Render(ComponentList("c", "b", "a"), _container);

        // Subtrees relocate; no span is remounted, and only the minimal moves are issued.
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div><span>c</span><span>b</span><span>a</span></div></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(2);
    }

    [Fact]
    public void KeyedFragments_Swap_MoveTheirWholeRange_NotRemount()
    {
        VirtualNode FragmentList(params string[] keys)
        {
            var children = new VirtualNode?[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                children[index] = VirtualNodeFactory.Fragment(
                    [VirtualNodeFactory.Element("span", keys[index])], keys[index]);
            }
            return VirtualNodeFactory.Element("div", null, children);
        }

        _renderer.Render(FragmentList("a", "b"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>a</span><span>b</span></div></root>");
        _renderer.OperationLog.Reset();

        _renderer.Render(FragmentList("b", "a"), _container);

        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>b</span><span>a</span></div></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(0);
    }

    [Fact]
    public void UnkeyedFragmentFlag_KeepsPositionalPatching_IgnoringKeys()
    {
        // With the UNKEYED_FRAGMENT flag the diff patches by index, so a per-position key change
        // remounts in place instead of moving — the contrast that proves the routing.
        VirtualNode UnkeyedFragment(params string[] keys)
        {
            var children = new VirtualNode?[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                children[index] = VirtualNodeFactory.Element(
                    "li", VirtualNodeFactory.Properties(("key", keys[index])), keys[index]);
            }
            return VirtualNodeFactory.Fragment(children, null, PatchFlags.UnkeyedFragment);
        }

        _renderer.Render(UnkeyedFragment("a", "b"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(UnkeyedFragment("b", "a"), _container);

        TestNodeSerializer.Serialize(_container).ShouldBe("<root><li>b</li><li>a</li></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(2);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(2);
    }

    [Fact]
    public void DuplicateKeys_ProduceADevWarning()
    {
        using var warnings = new WarningCapture();
        _renderer.Render(KeyedList("start"), _container);

        _renderer.Render(KeyedList("dup", "dup"), _container);

        warnings.Messages.ShouldContain(message => message.Contains("Duplicate keys"));
    }

    [Fact]
    public void MixedKeyedAndUnkeyedChildren_ProduceADevWarning()
    {
        using var warnings = new WarningCapture();
        var keyedOnly = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Element("li", VirtualNodeFactory.Properties(("key", "a")), "a"),
            VirtualNodeFactory.Element("li", VirtualNodeFactory.Properties(("key", "b")), "b"));
        _renderer.Render(keyedOnly, _container);

        // The reconciled middle mixes a keyless <span> with a keyed <li>.
        var mixed = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Element("span", "x"),
            VirtualNodeFactory.Element("li", VirtualNodeFactory.Properties(("key", "a")), "a"));
        _renderer.Render(mixed, _container);

        warnings.Messages.ShouldContain(message => message.Contains("Mixed keyed and unkeyed"));
    }

    [Fact]
    public void KeyedDiff_ConvergesForRandomPermutationsWithInsertsAndDeletes()
    {
        // Fuzz: any permutation with inserts and deletes must converge to the exact target order.
        var random = new Random(20260717);
        for (var iteration = 0; iteration < 400; iteration++)
        {
            var renderer = new TestRenderer();
            var container = renderer.CreateContainer();
            var initial = RandomDistinctKeys(random, 0, 15);
            renderer.Render(KeyedList(initial), container);
            RenderedKeys(container).ShouldBe(initial);

            var next = RandomNextKeys(random, initial);
            renderer.Render(KeyedList(next), container);
            RenderedKeys(container).ShouldBe(next, $"iteration {iteration}: [{string.Join(",", initial)}] -> [{string.Join(",", next)}]");
        }
    }

    private static VirtualNode KeyedList(params string[] keys)
    {
        var children = new VirtualNode?[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            children[index] = VirtualNodeFactory.Element(
                "li", VirtualNodeFactory.Properties(("key", keys[index])), keys[index]);
        }
        return VirtualNodeFactory.Element("ul", null, children);
    }

    private static string[] RenderedKeys(TestElement container)
    {
        var host = (TestElement)container.Children[0];
        return host.Children
            .OfType<TestElement>()
            .Select(child => ((TestText)child.Children[0]).Text)
            .ToArray();
    }

    private static string[] RandomDistinctKeys(Random random, int rangeStart, int rangeEnd)
    {
        var count = random.Next(1, 9);
        return Enumerable.Range(rangeStart, rangeEnd - rangeStart)
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(key => key.ToString())
            .ToArray();
    }

    private static string[] RandomNextKeys(Random random, string[] initial)
    {
        // Keep a random subset of the current keys (deletes), then splice in disjoint new keys
        // (inserts), then shuffle (reorder).
        var kept = initial.Where(_ => random.Next(3) != 0).ToList();
        var additions = Enumerable.Range(100, 15)
            .Where(_ => random.Next(3) == 0)
            .Select(key => key.ToString());
        return kept.Concat(additions).OrderBy(_ => random.Next()).ToArray();
    }
}
