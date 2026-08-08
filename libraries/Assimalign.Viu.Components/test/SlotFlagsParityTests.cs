using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

// Pins the SlotFlags values — a frozen contract between compiled output and the runtime,
// additive only. SlotFlags is a plain enumeration, not a bitmask ([RND-FLAGS-5]).
public class SlotFlagsParityTests
{
    [Theory]
    [InlineData(SlotFlags.Stable, 1)]
    [InlineData(SlotFlags.Dynamic, 2)]
    [InlineData(SlotFlags.Forwarded, 3)]
    public void EveryFlag_MatchesFrozenValueBitForBit(SlotFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Fact]
    public void FlagInventory_IsExhaustiveAndMatchesFrozenContract()
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
