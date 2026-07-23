using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Shared.Tests;

public class SlotFlagsParityTests
{
    [Theory]
    [InlineData(SlotFlags.Stable, 1)]
    [InlineData(SlotFlags.Dynamic, 2)]
    [InlineData(SlotFlags.Forwarded, 3)]
    public void Every_flag_matches_upstream_value_bit_for_bit(SlotFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Fact]
    public void Flag_inventory_is_exhaustive_and_matches_upstream_exactly()
    {
        var defined = Enum.GetValues<SlotFlags>();

        defined.Length.ShouldBe(3);
        defined.ShouldBe([SlotFlags.Stable, SlotFlags.Dynamic, SlotFlags.Forwarded], ignoreOrder: true);
    }

    [Fact]
    public void SlotFlags_is_a_plain_enumeration_not_a_bitmask()
    {
        typeof(SlotFlags).IsDefined(typeof(FlagsAttribute), inherit: false).ShouldBeFalse();
        Enum.GetUnderlyingType(typeof(SlotFlags)).ShouldBe(typeof(int));
    }
}
