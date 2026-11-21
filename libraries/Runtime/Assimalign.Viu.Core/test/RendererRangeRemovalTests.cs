using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins how a host range is removed: every sibling is resolved before the first removal, so a
/// host that buffers writes and flushes them on each read pays one flush for the whole range
/// instead of one per node. Specified by <c>[RND-IO-1]</c>.
/// </summary>
public sealed class RendererRangeRemovalTests
{
    [Fact]
    public void Unmount_FragmentRange_ResolvesEverySiblingBeforeTheFirstRemoval()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        FragmentNode root = new([new TextNode("a"), new TextNode("b"), new TextNode("c")]);
        renderer.Render(root, host.Container);
        int rangeLength = host.Container.Children.Count;
        rangeLength.ShouldBeGreaterThanOrEqualTo(3);
        host.ResetOperationCounts();
        var failure = new NotSupportedException("host removal failed");
        host.RemovalFailure = failure;

        Action unmount = () => renderer.Render(null, host.Container);

        unmount.ShouldThrow<NotSupportedException>().ShouldBeSameAs(failure);
        // Every sibling after the first node was read before the (failing) first removal.
        host.SiblingReadCount.ShouldBe(rangeLength - 1);
        host.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public void Unmount_FragmentRange_RemovesEveryNodeOnce()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        FragmentNode root = new([new TextNode("a"), new TextNode("b"), new TextNode("c")]);
        renderer.Render(root, host.Container);
        int rangeLength = host.Container.Children.Count;
        host.ResetOperationCounts();

        renderer.Render(null, host.Container);

        host.Container.Children.ShouldBeEmpty();
        host.RemoveCount.ShouldBe(rangeLength);
        host.SiblingReadCount.ShouldBe(rangeLength - 1);
    }
}
