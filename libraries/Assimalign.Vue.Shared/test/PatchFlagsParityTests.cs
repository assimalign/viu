using System;

using Shouldly;
using Xunit;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Shared.Tests;

public class PatchFlagsParityTests
{
    [Theory]
    [InlineData(PatchFlags.Text, 1)]
    [InlineData(PatchFlags.Class, 1 << 1)]
    [InlineData(PatchFlags.Style, 1 << 2)]
    [InlineData(PatchFlags.Props, 1 << 3)]
    [InlineData(PatchFlags.FullProps, 1 << 4)]
    [InlineData(PatchFlags.NeedHydration, 1 << 5)]
    [InlineData(PatchFlags.StableFragment, 1 << 6)]
    [InlineData(PatchFlags.KeyedFragment, 1 << 7)]
    [InlineData(PatchFlags.UnkeyedFragment, 1 << 8)]
    [InlineData(PatchFlags.NeedPatch, 1 << 9)]
    [InlineData(PatchFlags.DynamicSlots, 1 << 10)]
    [InlineData(PatchFlags.DevRootFragment, 1 << 11)]
    [InlineData(PatchFlags.Cached, -1)]
    [InlineData(PatchFlags.Bail, -2)]
    public void Every_flag_matches_upstream_value_bit_for_bit(PatchFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Theory]
    [InlineData(PatchFlags.Text, 1)]
    [InlineData(PatchFlags.Class, 2)]
    [InlineData(PatchFlags.Style, 4)]
    [InlineData(PatchFlags.Props, 8)]
    [InlineData(PatchFlags.FullProps, 16)]
    [InlineData(PatchFlags.NeedHydration, 32)]
    [InlineData(PatchFlags.StableFragment, 64)]
    [InlineData(PatchFlags.KeyedFragment, 128)]
    [InlineData(PatchFlags.UnkeyedFragment, 256)]
    [InlineData(PatchFlags.NeedPatch, 512)]
    [InlineData(PatchFlags.DynamicSlots, 1024)]
    [InlineData(PatchFlags.DevRootFragment, 2048)]
    public void Positive_flags_match_the_literal_upstream_table(PatchFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Fact]
    public void Flag_inventory_is_exhaustive_and_matches_upstream_exactly()
    {
        var defined = Enum.GetValues<PatchFlags>();

        defined.Length.ShouldBe(14);
        defined.ShouldBe(
            [
                PatchFlags.Text,
                PatchFlags.Class,
                PatchFlags.Style,
                PatchFlags.Props,
                PatchFlags.FullProps,
                PatchFlags.NeedHydration,
                PatchFlags.StableFragment,
                PatchFlags.KeyedFragment,
                PatchFlags.UnkeyedFragment,
                PatchFlags.NeedPatch,
                PatchFlags.DynamicSlots,
                PatchFlags.DevRootFragment,
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
    public void Positive_flags_combine_with_bitwise_or_as_upstream_patch_logic_expects()
    {
        var combined = PatchFlags.Text | PatchFlags.Class | PatchFlags.Style;

        ((int)combined).ShouldBe(1 | 2 | 4);
        (combined & PatchFlags.Text).ShouldBe(PatchFlags.Text);
        (combined & PatchFlags.Props).ShouldBe((PatchFlags)0);
    }
}
