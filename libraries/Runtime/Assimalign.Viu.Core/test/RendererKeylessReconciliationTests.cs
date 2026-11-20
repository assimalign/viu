using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins keyless sibling reconciliation: siblings without keys match the lowest unclaimed keyless
/// sibling of the same node type, in order, exactly as a scan from the first sibling would.
/// Specified by <c>[RND-KEY-3]</c>.
/// </summary>
public sealed class RendererKeylessReconciliationTests
{
    [Fact]
    public void Render_KeylessSiblingsAppendedThenTruncated_ReconcilePositionallyWithoutMoves()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(Items("a", "b", "c"), host.Container);
        RendererParityNode list = host.Container.Children.ShouldHaveSingleItem();
        RendererParityNode[] original = list.Children.ToArray();
        host.ResetOperationCounts();

        renderer.Render(Items("a", "b", "c", "d"), host.Container);

        list.DescendantText.ShouldBe("abcd");
        list.Children.Take(3).ShouldBe(original);
        host.MoveCount.ShouldBe(0);
        host.RemoveCount.ShouldBe(0);
        host.ResetOperationCounts();

        renderer.Render(Items("a", "c"), host.Container);

        list.DescendantText.ShouldBe("ac");
        list.Children[0].ShouldBeSameAs(original[0]);
        list.Children[1].ShouldBeSameAs(original[1]);
        host.MoveCount.ShouldBe(0);
        host.RemoveCount.ShouldBe(2);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_KeylessSiblingsOfMixedTypes_MatchByTypeAcrossClaimedPositions()
    {
        // text, element, text  ->  element, text, element: each previous sibling takes the first
        // unclaimed sibling of its own type, so the element skips the position its text sibling
        // claimed and the trailing text has nothing left to match.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            List(new TextNode("t1"), Item("e1"), new TextNode("t2")),
            host.Container);
        RendererParityNode list = host.Container.Children.ShouldHaveSingleItem();
        RendererParityNode originalText = list.Children[0];
        RendererParityNode originalElement = list.Children[1];
        host.ResetOperationCounts();

        renderer.Render(
            List(Item("e2"), new TextNode("t3"), Item("e3")),
            host.Container);

        list.Children.Count.ShouldBe(3);
        list.Children[0].ShouldBeSameAs(originalElement);
        list.Children[0].DescendantText.ShouldBe("e2");
        list.Children[1].ShouldBeSameAs(originalText);
        list.Children[1].Text.ShouldBe("t3");
        list.Children[2].Description.ShouldBe("item");
        list.Children[2].DescendantText.ShouldBe("e3");
        host.RemoveCount.ShouldBe(1);
        host.MoveCount.ShouldBe(1);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_KeylessSiblingBehindKeyedSibling_SkipsKeyedPositionsWhenMatching()
    {
        // A keyless search never claims a keyed position, even when the keyed sibling moves.
        ElementNode initial = List(Item("a", key: "a"), Item("x"));
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
        RendererParityNode list = host.Container.Children.ShouldHaveSingleItem();
        RendererParityNode keyed = list.Children[0];
        RendererParityNode keyless = list.Children[1];

        renderer.Render(List(Item("y"), Item("a", key: "a")), host.Container);

        list.Children.Count.ShouldBe(2);
        list.Children[0].ShouldBeSameAs(keyless);
        list.Children[0].DescendantText.ShouldBe("y");
        list.Children[1].ShouldBeSameAs(keyed);
        list.Children[1].DescendantText.ShouldBe("a");
        warnings.ShouldAllBe(warning => warning.Contains("mixes keyed and keyless"));
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    private static ElementNode Items(params string[] texts)
    {
        var children = new VirtualNode[texts.Length];
        for (int index = 0; index < texts.Length; index++)
        {
            children[index] = Item(texts[index]);
        }

        return List(children);
    }

    private static ElementNode Item(string text, object? key = null) =>
        new(new QualifiedName("item"), children: [new TextNode(text)], key: key);

    private static ElementNode List(params VirtualNode[] children) =>
        new(new QualifiedName("list"), children: children);
}
