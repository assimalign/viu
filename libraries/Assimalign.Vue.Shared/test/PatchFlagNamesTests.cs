using Shouldly;
using Xunit;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Shared.Tests;

public class PatchFlagNamesTests
{
    [Theory]
    [InlineData(PatchFlags.Text, "TEXT")]
    [InlineData(PatchFlags.Class, "CLASS")]
    [InlineData(PatchFlags.Style, "STYLE")]
    [InlineData(PatchFlags.Props, "PROPS")]
    [InlineData(PatchFlags.FullProps, "FULL_PROPS")]
    [InlineData(PatchFlags.NeedHydration, "NEED_HYDRATION")]
    [InlineData(PatchFlags.StableFragment, "STABLE_FRAGMENT")]
    [InlineData(PatchFlags.KeyedFragment, "KEYED_FRAGMENT")]
    [InlineData(PatchFlags.UnkeyedFragment, "UNKEYED_FRAGMENT")]
    [InlineData(PatchFlags.NeedPatch, "NEED_PATCH")]
    [InlineData(PatchFlags.DynamicSlots, "DYNAMIC_SLOTS")]
    [InlineData(PatchFlags.DevRootFragment, "DEV_ROOT_FRAGMENT")]
    [InlineData(PatchFlags.Cached, "CACHED")]
    [InlineData(PatchFlags.Bail, "BAIL")]
    public void Every_flag_formats_as_its_upstream_name(PatchFlags flag, string expected)
    {
        PatchFlagNames.Format(flag).ShouldBe(expected);
    }

    [Fact]
    public void Combined_flags_format_as_a_comma_separated_list_in_bit_order()
    {
        PatchFlagNames.Format(PatchFlags.Text | PatchFlags.Class).ShouldBe("TEXT, CLASS");
        PatchFlagNames.Format(PatchFlags.Class | PatchFlags.Style | PatchFlags.Props)
            .ShouldBe("CLASS, STYLE, PROPS");
        PatchFlagNames.Format(PatchFlags.StableFragment | PatchFlags.DevRootFragment)
            .ShouldBe("STABLE_FRAGMENT, DEV_ROOT_FRAGMENT");
        PatchFlagNames.Format(PatchFlags.Text | PatchFlags.NeedHydration | PatchFlags.DynamicSlots)
            .ShouldBe("TEXT, NEED_HYDRATION, DYNAMIC_SLOTS");
    }

    [Fact]
    public void All_positive_flags_combined_format_in_ascending_bit_order()
    {
        var all = PatchFlags.Text | PatchFlags.Class | PatchFlags.Style | PatchFlags.Props
            | PatchFlags.FullProps | PatchFlags.NeedHydration | PatchFlags.StableFragment
            | PatchFlags.KeyedFragment | PatchFlags.UnkeyedFragment | PatchFlags.NeedPatch
            | PatchFlags.DynamicSlots | PatchFlags.DevRootFragment;

        PatchFlagNames.Format(all).ShouldBe(
            "TEXT, CLASS, STYLE, PROPS, FULL_PROPS, NEED_HYDRATION, STABLE_FRAGMENT, "
            + "KEYED_FRAGMENT, UNKEYED_FRAGMENT, NEED_PATCH, DYNAMIC_SLOTS, DEV_ROOT_FRAGMENT");
    }

    [Fact]
    public void Values_with_no_known_flag_format_as_their_numeric_value()
    {
        PatchFlagNames.Format((PatchFlags)0).ShouldBe("0");
        PatchFlagNames.Format((PatchFlags)(1 << 12)).ShouldBe("4096");
        PatchFlagNames.Format((PatchFlags)(-3)).ShouldBe("-3");
    }
}
