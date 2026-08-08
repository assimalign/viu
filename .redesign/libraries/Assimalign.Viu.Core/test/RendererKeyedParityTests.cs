using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins keyed reconciliation identity, diagnostics, and longest-increasing-subsequence behavior
/// specified by <c>[RND-KEY-3]</c> and <c>[RND-KEY-4]</c>.
/// </summary>
public sealed class RendererKeyedParityTests
{
    [Fact]
    public void Render_ReversedKeyedList_PreservesIdentityWithMinimalMoves()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(KeyedList("1", "2", "3", "4", "5"), host.Container);
        Dictionary<string, RendererParityNode> original = RenderedElements(host)
            .ToDictionary(node => node.DescendantText, StringComparer.Ordinal);
        host.ResetOperationCounts();

        renderer.Render(KeyedList("5", "4", "3", "2", "1"), host.Container);

        RendererParityNode[] reordered = RenderedElements(host);
        reordered.Select(node => node.DescendantText)
            .ShouldBe(["5", "4", "3", "2", "1"]);
        for (int index = 0; index < reordered.Length; index++)
        {
            string key = reordered[index].DescendantText;
            reordered[index].ShouldBeSameAs(original[key]);
        }

        host.MoveCount.ShouldBe(4);
        host.InsertCount.ShouldBe(0);
        host.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public void Render_SameKeyWithDifferentNodeType_DoesNotMoveRetainedSibling()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            KeyedList(
                KeyedElement("first-kind", "a", "A"),
                KeyedElement("item", "b", "B")),
            host.Container);
        RendererParityNode retained = RenderedElements(host)
            .Single(node => node.DescendantText == "B");
        host.ResetOperationCounts();

        renderer.Render(
            KeyedList(
                KeyedElement("item", "b", "B"),
                KeyedElement("replacement-kind", "a", "A")),
            host.Container);

        RendererParityNode[] rendered = RenderedElements(host);
        rendered.Select(node => node.DescendantText).ShouldBe(["B", "A"]);
        rendered[0].ShouldBeSameAs(retained);
        rendered[1].Description.ShouldBe("replacement-kind");
        host.MoveCount.ShouldBe(0);
    }

    [Fact]
    public void Render_UnkeyedFragmentFlag_PatchesPositionsWithoutMovingHostNodes()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(UnkeyedFragment("A", "B"), host.Container);
        RendererParityNode[] original = host.Container.Children
            .Where(node => node.Kind == RendererParityNodeKind.Element)
            .ToArray();
        host.ResetOperationCounts();

        renderer.Render(UnkeyedFragment("B", "A"), host.Container);

        RendererParityNode[] rendered = host.Container.Children
            .Where(node => node.Kind == RendererParityNodeKind.Element)
            .ToArray();
        rendered.Select(node => node.DescendantText).ShouldBe(["B", "A"]);
        rendered[0].ShouldBeSameAs(original[0]);
        rendered[1].ShouldBeSameAs(original[1]);
        host.MoveCount.ShouldBe(0);
        host.InsertCount.ShouldBe(0);
        host.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public void Render_KeyedFragmentReorder_MovesTheCompleteHostRangeAsOneUnit()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        FragmentNode fragment = KeyedFragment("fragment");
        ElementNode sibling = KeyedElement("item", "sibling", "B");
        renderer.Render(KeyedList(sibling, fragment), host.Container);
        RendererParityNode list = host.Container.Children.Single();
        RendererParityNode originalSibling = list.Children[0];
        RendererParityNode[] originalRange = list.Children.Skip(1).Take(4).ToArray();
        host.ResetOperationCounts();

        renderer.Render(
            KeyedList(
                KeyedFragment("fragment"),
                KeyedElement("item", "sibling", "B")),
            host.Container);

        list.Children.Take(4).ShouldBe(originalRange);
        list.Children[4].ShouldBeSameAs(originalSibling);
        host.MoveCount.ShouldBe(4);
        host.InsertCount.ShouldBe(0);
        host.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public void Render_RandomKeyedPermutationsWithInsertsAndDeletes_PreserveRetainedIdentity()
    {
        var random = new Random(20260717);
        for (int iteration = 0; iteration < 100; iteration++)
        {
            using var host = new RendererParityHost();
            Renderer<RendererParityNode> renderer = host.CreateRenderer();
            string[] initial = RandomDistinctKeys(random, 0, 15);
            renderer.Render(KeyedList(initial), host.Container);
            Dictionary<string, RendererParityNode> original = RenderedElements(host)
                .ToDictionary(node => node.DescendantText, StringComparer.Ordinal);
            string[] next = RandomNextKeys(random, initial);

            renderer.Render(KeyedList(next), host.Container);

            RendererParityNode[] rendered = RenderedElements(host);
            rendered.Select(node => node.DescendantText).ShouldBe(
                next,
                $"iteration {iteration}: "
                + $"[{string.Join(",", initial)}] -> "
                + $"[{string.Join(",", next)}]");
            for (int index = 0; index < next.Length; index++)
            {
                if (original.TryGetValue(next[index], out RendererParityNode? retained))
                {
                    rendered[index].ShouldBeSameAs(
                        retained,
                        $"retained key {next[index]} remounted during iteration {iteration}");
                }
            }
        }
    }

    [Fact]
    public void Render_DuplicateKeys_WarnsThroughApplicationContext()
    {
        ElementNode initial = KeyedList("initial");
        List<string> warnings = [];
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = initial,
                WarnHandler = warnings.Add,
            });
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, application);

        renderer.Render(KeyedList("duplicate", "duplicate"), host.Container);

        warnings.ShouldContain(
            warning => warning.Contains("Duplicate sibling key", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MixedKeyedAndKeylessChildren_WarnsWhileCommentsRemainExempt()
    {
        ElementNode initial = KeyedList("a", "b");
        List<string> warnings = [];
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = initial,
                WarnHandler = warnings.Add,
            });
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(initial, host.Container, application);

        renderer.Render(
            new ElementNode(
                new QualifiedName("list"),
                children:
                [
                    new CommentNode(string.Empty),
                    KeyedElement("a"),
                ]),
            host.Container);

        warnings.ShouldBeEmpty();

        renderer.Render(
            new ElementNode(
                new QualifiedName("list"),
                children:
                [
                    new ElementNode(
                        new QualifiedName("item"),
                        children: [new TextNode("keyless")]),
                    KeyedElement("a"),
                ]),
            host.Container);

        warnings.ShouldContain(
            warning => warning.Contains(
                "mixes keyed and keyless",
                StringComparison.Ordinal));
    }

    private static ElementNode KeyedList(params string[] keys)
    {
        var children = new List<VirtualNode>(keys.Length);
        for (int index = 0; index < keys.Length; index++)
        {
            children.Add(KeyedElement(keys[index]));
        }

        return new ElementNode(new QualifiedName("list"), children: children);
    }

    private static ElementNode KeyedList(params VirtualNode[] children) =>
        new(new QualifiedName("list"), children: children);

    private static ElementNode KeyedElement(string key) =>
        KeyedElement("item", key, key);

    private static ElementNode KeyedElement(string name, string key, string text) =>
        new(new QualifiedName(name), children: [new TextNode(text)], key: key);

    private static FragmentNode KeyedFragment(string key) =>
        new(
            children:
            [
                new ElementNode(
                    new QualifiedName("fragment-item"),
                    children: [new TextNode("A1")]),
                new ElementNode(
                    new QualifiedName("fragment-item"),
                    children: [new TextNode("A2")]),
            ],
            key: key);

    private static FragmentNode UnkeyedFragment(params string[] values)
    {
        var children = new List<VirtualNode>(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            children.Add(
                new ElementNode(
                    new QualifiedName("item"),
                    children: [new TextNode(values[index])]));
        }

        return new FragmentNode(
            children,
            renderPlan: new RenderPlan(PatchFlags.UnkeyedFragment));
    }

    private static RendererParityNode[] RenderedElements(RendererParityHost host) =>
        host.Container.Children
            .Single()
            .Children
            .Where(node => node.Kind == RendererParityNodeKind.Element)
            .ToArray();

    private static string[] RandomDistinctKeys(
        Random random,
        int rangeStart,
        int rangeEnd)
    {
        int count = random.Next(1, 9);
        return Enumerable.Range(rangeStart, rangeEnd - rangeStart)
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(key => key.ToString())
            .ToArray();
    }

    private static string[] RandomNextKeys(Random random, string[] initial)
    {
        IEnumerable<string> retained = initial.Where(_ => random.Next(3) != 0);
        IEnumerable<string> additions = Enumerable.Range(100, 15)
            .Where(_ => random.Next(3) == 0)
            .Select(key => key.ToString());
        return retained
            .Concat(additions)
            .OrderBy(_ => random.Next())
            .ToArray();
    }
}
