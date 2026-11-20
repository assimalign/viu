using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins how a block keeps its dynamic-children association honest across patches and teardown.
/// Specified by <c>[RND-BLOCK-4]</c> and <c>[RND-BLOCK-5]</c>.
/// </summary>
public sealed class RendererBlockVerificationTests
{
    [Fact]
    public void Render_AliasAppearsAfterBlockPatch_FallsBackToOccurrenceDiffOnTheNextRender()
    {
        // [RND-BLOCK-4] After a block patch, a now-tracked description that also sits at an
        // untracked position can no longer be told apart by identity, so the next render must take
        // the occurrence diff instead of trusting the stale positional association.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ElementNode first = Signal("first");
        ElementNode second = Signal("second");
        ElementNode cached = Signal("cached");
        renderer.Render(RepeatedBlock([first, second, cached], [first, second]), host.Container);
        RendererParityNode list = host.Container.Children.ShouldHaveSingleItem();
        RendererParityNode[] originalSignals = list.Children
            .Select(row => row.Children.ShouldHaveSingleItem())
            .ToArray();

        renderer.Render(RepeatedBlock([cached, second, cached], [cached, second]), host.Container);

        originalSignals[0].Bindings["data-state"].ShouldBe("cached");
        originalSignals[2].Bindings["data-state"].ShouldBe("cached");

        ElementNode changed = Signal("changed");
        renderer.Render(RepeatedBlock([cached, second, changed], [cached, second]), host.Container);

        RendererParityNode[] patchedSignals = list.Children
            .Select(row => row.Children.ShouldHaveSingleItem())
            .ToArray();
        patchedSignals.ShouldBe(originalSignals);
        patchedSignals[0].Bindings["data-state"].ShouldBe("cached");
        patchedSignals[1].Bindings["data-state"].ShouldBe("second");
        patchedSignals[2].Bindings["data-state"].ShouldBe("changed");
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_BlockPatchWithoutAlias_KeepsPatchingOnlyTrackedPositions()
    {
        // [RND-BLOCK-5] The verification after a block patch must not disturb an unambiguous
        // association: later renders still visit only the tracked positions.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ElementNode first = Signal("first");
        ElementNode second = Signal("second");
        ElementNode untracked = Signal("untracked");
        renderer.Render(RepeatedBlock([first, second, untracked], [first, second]), host.Container);
        renderer.Render(RepeatedBlock([Signal("first-2"), second, untracked], [Signal("first-2"), second]), host.Container);
        RendererParityNode list = host.Container.Children.ShouldHaveSingleItem();
        Renderer<RendererParityNode>.PatchVisitCount = 0;

        ElementNode third = Signal("first-3");
        renderer.Render(RepeatedBlock([third, second, untracked], [third, second]), host.Container);

        // The list itself plus the two tracked positions; the untracked third row is never visited.
        Renderer<RendererParityNode>.PatchVisitCount.ShouldBe(3);
        list.Children[0].Children.ShouldHaveSingleItem().Bindings["data-state"].ShouldBe("first-3");
        list.Children[2].Children.ShouldHaveSingleItem().Bindings["data-state"].ShouldBe("untracked");
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(40)]
    public void Render_BlockTeardown_UnmountsEveryTrackedChildAndReleasesTheRest(int rowCount)
    {
        // [RND-BLOCK-5] Teardown visits each tracked child exactly once and releases the static
        // rows without a visit, whether the block is small enough to scan or large enough to hash.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        VirtualNode[] signals = new VirtualNode[rowCount];
        for (int index = 0; index < rowCount; index++)
        {
            signals[index] = Signal($"signal-{index}");
        }

        renderer.Render(RepeatedBlock(signals, signals), host.Container);
        host.Container.Children.ShouldHaveSingleItem().Children.Count.ShouldBe(rowCount);
        host.ResetOperationCounts();
        Renderer<RendererParityNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Container);

        host.Container.Children.ShouldBeEmpty();
        Renderer<RendererParityNode>.UnmountVisitCount.ShouldBe(rowCount + 1);
        host.RemoveCount.ShouldBe(1);
    }

    private static ElementNode Signal(string state) => new(
        new QualifiedName("signal"),
        bindings: [ElementBinding.Attribute(new QualifiedName("data-state"), state)],
        renderPlan: new RenderPlan(PatchFlags.FullProperties));

    private static ElementNode RepeatedBlock(
        IReadOnlyList<VirtualNode> values,
        IReadOnlyList<VirtualNode> dynamicChildren)
    {
        var rows = new List<VirtualNode>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            rows.Add(
                new ElementNode(
                    new QualifiedName("row"),
                    children: [values[index]],
                    key: index));
        }

        return new ElementNode(
            new QualifiedName("list"),
            children: rows,
            renderPlan: new RenderPlan(PatchFlags.NeedPatch, dynamicChildren: dynamicChildren));
    }
}
