using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

// Pins the PatchFlags bit layout. The values are a frozen contract between compiled output and
// the runtime: changing one silently breaks components compiled by an earlier Viu, so values are
// additive only and this table is the guard against an accidental renumbering ([RND-FLAGS-1],
// [RND-FLAGS-3]).
public class PatchFlagsParityTests
{
    [Theory]
    [InlineData(PatchFlags.Text, 1)]
    [InlineData(PatchFlags.Class, 1 << 1)]
    [InlineData(PatchFlags.Style, 1 << 2)]
    [InlineData(PatchFlags.Properties, 1 << 3)]
    [InlineData(PatchFlags.FullProperties, 1 << 4)]
    [InlineData(PatchFlags.NeedsHydration, 1 << 5)]
    [InlineData(PatchFlags.StableFragment, 1 << 6)]
    [InlineData(PatchFlags.KeyedFragment, 1 << 7)]
    [InlineData(PatchFlags.UnkeyedFragment, 1 << 8)]
    [InlineData(PatchFlags.NeedPatch, 1 << 9)]
    [InlineData(PatchFlags.DynamicSlots, 1 << 10)]
    [InlineData(PatchFlags.DevelopmentRootFragment, 1 << 11)]
    [InlineData(PatchFlags.Cached, -1)]
    [InlineData(PatchFlags.Bail, -2)]
    public void EveryFlag_MatchesFrozenValueBitForBit(PatchFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Theory]
    [InlineData(PatchFlags.Text, 1)]
    [InlineData(PatchFlags.Class, 2)]
    [InlineData(PatchFlags.Style, 4)]
    [InlineData(PatchFlags.Properties, 8)]
    [InlineData(PatchFlags.FullProperties, 16)]
    [InlineData(PatchFlags.NeedsHydration, 32)]
    [InlineData(PatchFlags.StableFragment, 64)]
    [InlineData(PatchFlags.KeyedFragment, 128)]
    [InlineData(PatchFlags.UnkeyedFragment, 256)]
    [InlineData(PatchFlags.NeedPatch, 512)]
    [InlineData(PatchFlags.DynamicSlots, 1024)]
    [InlineData(PatchFlags.DevelopmentRootFragment, 2048)]
    public void PositiveFlags_MatchFrozenValueTable(PatchFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Fact]
    public void FlagInventory_IsExhaustiveAndMatchesFrozenContract()
    {
        var defined = Enum.GetValues<PatchFlags>();

        defined.Length.ShouldBe(15);
        defined.ShouldBe(
            [
                PatchFlags.None,
                PatchFlags.Text,
                PatchFlags.Class,
                PatchFlags.Style,
                PatchFlags.Properties,
                PatchFlags.FullProperties,
                PatchFlags.NeedsHydration,
                PatchFlags.StableFragment,
                PatchFlags.KeyedFragment,
                PatchFlags.UnkeyedFragment,
                PatchFlags.NeedPatch,
                PatchFlags.DynamicSlots,
                PatchFlags.DevelopmentRootFragment,
                PatchFlags.Cached,
                PatchFlags.Bail,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Enum_is_int_backed_so_negative_sentinels_round_trip()
    {
        Enum.GetUnderlyingType(typeof(PatchFlags)).ShouldBe(typeof(int));

        ((int)PatchFlags.Cached).ShouldBe(-1);
        ((PatchFlags)(-1)).ShouldBe(PatchFlags.Cached);
        ((int)PatchFlags.Bail).ShouldBe(-2);
        ((PatchFlags)(-2)).ShouldBe(PatchFlags.Bail);
    }

    [Fact]
    public void PositiveFlags_CombineWithBitwiseOrAsViuPatchingRequires()
    {
        var combined = PatchFlags.Text | PatchFlags.Class | PatchFlags.Style;

        ((int)combined).ShouldBe(1 | 2 | 4);
        (combined & PatchFlags.Text).ShouldBe(PatchFlags.Text);
        (combined & PatchFlags.Properties).ShouldBe((PatchFlags)0);
    }
}
