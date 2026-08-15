using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentRenderFrameTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_CompilerContract_UsesExactCacheSize(int renderCacheSize)
    {
        ComponentContract contract = new(renderCacheSize: renderCacheSize);

        ComponentRenderFrame frame = new(contract);

        frame.Cache.Length.ShouldBe(renderCacheSize);
    }

    [Fact]
    public void Constructor_LegacyContract_UsesCompatibilityCacheSize()
    {
        ComponentContract contract = new();

        ComponentRenderFrame frame = new(contract);

        frame.Cache.Length.ShouldBe(64);
    }

    [Fact]
    public void Constructor_UpdatedCompilerProvider_UsesCurrentExactCacheSizeOncePerMount()
    {
        // [V01.01.06.14]/[SFC-CG-9] A remount after a structural delta must allocate from the
        // updated provider body retained by the existing static contract.
        int currentSize = 1;
        int providerRuns = 0;
        ComponentContract contract = new(displayName: null, renderCacheSizeProvider: () =>
        {
            providerRuns++;
            return currentSize;
        });

        ComponentRenderFrame first = new(contract);
        currentSize = 4;
        ComponentRenderFrame second = new(contract);

        first.Cache.Length.ShouldBe(1);
        second.Cache.Length.ShouldBe(4);
        providerRuns.ShouldBe(2);
    }

    [Fact]
    public void CloseBlock_TwoTrackedNodes_ReturnsExactlyThoseNodesInOrder()
    {
        ComponentRenderFrame frame = new();
        TextNode first = new("first");
        TextNode second = new("second");

        frame.OpenBlock();
        frame.Track(first);
        frame.Track(second);
        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        block.ShouldNotBeNull();
        block.Count.ShouldBe(2);
        block[0].ShouldBeSameAs(first);
        block[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void CloseBlock_RepeatedTrackedReference_PreservesEveryOccurrenceInOrder()
    {
        // [RND-BLOCK-1]/[RND-BLOCK-3] The block contract is an occurrence list,
        // so compiler-cached aliases are never deduplicated by description identity.
        ComponentRenderFrame frame = new();
        TextNode cached = new("cached");

        frame.OpenBlock();
        frame.Track(cached);
        frame.Track(cached);
        frame.Track(cached);
        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        block.ShouldNotBeNull();
        block.Count.ShouldBe(3);
        block.ShouldAllBe(node => ReferenceEquals(node, cached));
    }

    [Fact]
    public void CloseBlock_NestedBlocks_EachBlockIsolatesItsOwnDynamicChildren()
    {
        ComponentRenderFrame frame = new();
        TextNode outerNode = new("outer");
        TextNode innerNode = new("inner");

        frame.OpenBlock();
        frame.Track(outerNode);
        frame.OpenBlock();
        frame.Track(innerNode);
        IReadOnlyList<VirtualNode>? inner = frame.CloseBlock();
        IReadOnlyList<VirtualNode>? outer = frame.CloseBlock();

        inner.ShouldNotBeNull();
        outer.ShouldNotBeNull();
        inner.ShouldHaveSingleItem().ShouldBeSameAs(innerNode);
        outer.ShouldHaveSingleItem().ShouldBeSameAs(outerNode);
    }

    [Fact]
    public void CloseBlock_DisabledBlock_ReturnsEmptyOptimizedBlock()
    {
        ComponentRenderFrame frame = new();

        frame.OpenBlock(disableTracking: true);
        frame.Track(new TextNode("ignored"));

        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        // Local collection suppression remains an optimized empty block [RND-BLOCK-2].
        block.ShouldNotBeNull();
        block.ShouldBeEmpty();
    }

    [Fact]
    public void SetBlockTracking_NestedSuspension_TracksOnlyAfterBalancedResume()
    {
        ComponentRenderFrame frame = new();
        TextNode ignoredFirst = new("ignored-first");
        TextNode ignoredSecond = new("ignored-second");
        TextNode tracked = new("tracked");

        frame.OpenBlock();
        frame.SetBlockTracking(-1);
        frame.Track(ignoredFirst);
        frame.SetBlockTracking(-1);
        frame.SetBlockTracking(1);
        frame.Track(ignoredSecond);
        frame.SetBlockTracking(1);
        frame.Track(tracked);
        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        block.ShouldNotBeNull();
        block.ShouldHaveSingleItem().ShouldBeSameAs(tracked);
    }

    [Fact]
    public void CloseBlock_SuspendedGlobalTracking_ReturnsNullForFullWalk()
    {
        ComponentRenderFrame frame = new();
        frame.OpenBlock();
        frame.SetBlockTracking(-1);

        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        // Global suspension removes block metadata and therefore requires a full walk [RND-BLOCK-2].
        block.ShouldBeNull();
    }

    [Fact]
    public void CloseBlock_NeverOpenedFrame_ThrowsInvalidOperationException()
    {
        ComponentRenderFrame frame = new();

        Should.Throw<InvalidOperationException>(() => frame.CloseBlock());
    }

    [Fact]
    public void GetOrAddCache_NullValue_InvokesFactoryOnlyOnce()
    {
        ComponentRenderFrame frame = new(cacheSize: 1);
        int runs = 0;

        object? first = frame.GetOrAddCache<object?>(0, () =>
        {
            runs++;
            return null;
        });
        object? second = frame.GetOrAddCache<object?>(0, () =>
        {
            runs++;
            return new object();
        });

        first.ShouldBeNull();
        second.ShouldBeNull();
        runs.ShouldBe(1);
    }

    [Fact]
    public void CacheHandler_Factory_RetainsDelegateIdentityAndRunsOnce()
    {
        ComponentRenderFrame frame = new(cacheSize: 1);
        int factoryRuns = 0;
        Func<Action> factory = () =>
        {
            factoryRuns++;
            return () => { };
        };

        Action first = frame.CacheHandler(0, factory);
        Action second = frame.CacheHandler(0, factory);

        second.ShouldBeSameAs(first);
        factoryRuns.ShouldBe(1);
    }

    [Fact]
    public void Memo_UnchangedDependencies_ReusesNodeAndTracksCacheHit()
    {
        ComponentRenderFrame frame = new(cacheSize: 1);
        int renders = 0;
        TextNode first = (TextNode)frame.Memo(
            0,
            new object?[] { 1, "stable" },
            () =>
            {
                renders++;
                return new TextNode("cached");
            })!;

        frame.OpenBlock();
        VirtualNode? second = frame.Memo(
            0,
            new object?[] { 1, "stable" },
            () =>
            {
                renders++;
                return new TextNode("replacement");
            });
        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        second.ShouldBeSameAs(first);
        renders.ShouldBe(1);
        block.ShouldNotBeNull();
        block.ShouldHaveSingleItem().ShouldBeSameAs(first);
    }

    [Fact]
    public void Memo_ChangedDependency_ReplacesCachedNode()
    {
        ComponentRenderFrame frame = new(cacheSize: 1);
        VirtualNode? first = frame.Memo(0, new object?[] { 1 }, () => new TextNode("first"));
        VirtualNode? second = frame.Memo(0, new object?[] { 2 }, () => new TextNode("second"));

        second.ShouldNotBeSameAs(first);
        second.ShouldBeOfType<TextNode>().Text.ShouldBe("second");
    }

    [Fact]
    public void CacheOperations_OutOfRangeSlot_ThrowArgumentOutOfRangeException()
    {
        ComponentRenderFrame frame = new(cacheSize: 1);

        Should.Throw<ArgumentOutOfRangeException>(
            () => frame.GetOrAddCache(1, static () => new object()));
        Should.Throw<ArgumentOutOfRangeException>(
            () => frame.SetCache(-1, new object()));
    }
}
