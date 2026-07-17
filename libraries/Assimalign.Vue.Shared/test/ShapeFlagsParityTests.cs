using Assimalign.Vue.Shared;
using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

public class ShapeFlagsParityTests
{
    [Theory]
    [InlineData(ShapeFlags.Element, 1)]
    [InlineData(ShapeFlags.FunctionalComponent, 1 << 1)]
    [InlineData(ShapeFlags.StatefulComponent, 1 << 2)]
    [InlineData(ShapeFlags.TextChildren, 1 << 3)]
    [InlineData(ShapeFlags.ArrayChildren, 1 << 4)]
    [InlineData(ShapeFlags.SlotsChildren, 1 << 5)]
    [InlineData(ShapeFlags.Teleport, 1 << 6)]
    [InlineData(ShapeFlags.Suspense, 1 << 7)]
    [InlineData(ShapeFlags.ComponentShouldKeepAlive, 1 << 8)]
    [InlineData(ShapeFlags.ComponentKeptAlive, 1 << 9)]
    [InlineData(ShapeFlags.Component, (1 << 2) | (1 << 1))]
    public void Every_flag_matches_upstream_value_bit_for_bit(ShapeFlags flag, int expected)
    {
        ((int)flag).ShouldBe(expected);
    }

    [Fact]
    public void Component_is_the_composite_of_stateful_and_functional()
    {
        ShapeFlags.Component.ShouldBe(ShapeFlags.StatefulComponent | ShapeFlags.FunctionalComponent);
        ((int)ShapeFlags.Component).ShouldBe(6);
    }

    [Fact]
    public void Flag_inventory_is_exhaustive_and_matches_upstream_exactly()
    {
        var defined = Enum.GetValues<ShapeFlags>();

        defined.Length.ShouldBe(11);
        defined.ShouldBe(
            [
                ShapeFlags.Element,
                ShapeFlags.FunctionalComponent,
                ShapeFlags.StatefulComponent,
                ShapeFlags.TextChildren,
                ShapeFlags.ArrayChildren,
                ShapeFlags.SlotsChildren,
                ShapeFlags.Teleport,
                ShapeFlags.Suspense,
                ShapeFlags.ComponentShouldKeepAlive,
                ShapeFlags.ComponentKeptAlive,
                ShapeFlags.Component,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Enum_is_int_backed()
    {
        Enum.GetUnderlyingType(typeof(ShapeFlags)).ShouldBe(typeof(int));
    }
}
