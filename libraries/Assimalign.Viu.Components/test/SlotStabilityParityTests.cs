using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

// Pins the SlotStability values — a frozen contract between compiled output and the runtime,
// additive only. SlotStability is a plain enumeration, not a bitmask ([RND-FLAGS-5]).
public class SlotStabilityParityTests
{
    [Theory]
    [InlineData(SlotStability.Stable, 1)]
    [InlineData(SlotStability.Dynamic, 2)]
    [InlineData(SlotStability.Forwarded, 3)]
    public void EveryValue_MatchesFrozenValueBitForBit(SlotStability stability, int expected)
    {
        ((int)stability).ShouldBe(expected);
    }

    [Fact]
    public void ValueInventory_IsExhaustiveAndMatchesFrozenContract()
    {
        var defined = Enum.GetValues<SlotStability>();

        defined.Length.ShouldBe(3);
        defined.ShouldBe([SlotStability.Stable, SlotStability.Dynamic, SlotStability.Forwarded], ignoreOrder: true);
    }

    [Fact]
    public void SlotStability_IsPlainEnumerationNotBitmask()
    {
        typeof(SlotStability).IsDefined(typeof(FlagsAttribute), inherit: false).ShouldBeFalse();
        Enum.GetUnderlyingType(typeof(SlotStability)).ShouldBe(typeof(int));
    }
}
