using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentRenderFrameTests
{
    [Fact]
    public void CloseBlock_TwoTrackedNodes_ReturnsExactlyThoseNodesInOrder()
    {
        var frame = new ComponentRenderFrame();
        var first = new TextNode("first");
        var second = new TextNode("second");

        frame.OpenBlock();
        frame.Track(first);
        frame.Track(second);
        var block = frame.CloseBlock();

        block.Count.ShouldBe(2);
        block[0].ShouldBeSameAs(first);
        block[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void CloseBlock_NestedBlocks_EachBlockIsolatesItsOwnDynamicChildren()
    {
        var frame = new ComponentRenderFrame();
        var outerNode = new TextNode("outer");
        var innerNode = new TextNode("inner");

        frame.OpenBlock();
        frame.Track(outerNode);
        frame.OpenBlock();
        frame.Track(innerNode);
        var inner = frame.CloseBlock();
        var outer = frame.CloseBlock();

        inner.ShouldHaveSingleItem().ShouldBeSameAs(innerNode);
        outer.ShouldHaveSingleItem().ShouldBeSameAs(outerNode);
    }

    [Fact]
    public void CloseBlock_NeverOpenedFrame_ReturnsEmpty()
    {
        var frame = new ComponentRenderFrame();

        frame.CloseBlock().ShouldBeEmpty();
    }
}
