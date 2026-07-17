using Shouldly;
using Xunit;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Shared.Tests;

public class ShapeFlagsExtensionsTests
{
    [Fact]
    public void IsElement_matches_only_element_vnodes()
    {
        ShapeFlags.Element.IsElement().ShouldBeTrue();
        (ShapeFlags.Element | ShapeFlags.TextChildren).IsElement().ShouldBeTrue();
        ShapeFlags.StatefulComponent.IsElement().ShouldBeFalse();
        ShapeFlags.Teleport.IsElement().ShouldBeFalse();
    }

    [Fact]
    public void IsComponent_matches_both_component_kinds_via_the_composite_mask()
    {
        ShapeFlags.StatefulComponent.IsComponent().ShouldBeTrue();
        ShapeFlags.FunctionalComponent.IsComponent().ShouldBeTrue();
        (ShapeFlags.StatefulComponent | ShapeFlags.SlotsChildren).IsComponent().ShouldBeTrue();

        ShapeFlags.Element.IsComponent().ShouldBeFalse();
        ShapeFlags.Teleport.IsComponent().ShouldBeFalse();
        ShapeFlags.Suspense.IsComponent().ShouldBeFalse();
    }

    [Fact]
    public void Stateful_and_functional_predicates_distinguish_component_kinds()
    {
        ShapeFlags.StatefulComponent.IsStatefulComponent().ShouldBeTrue();
        ShapeFlags.StatefulComponent.IsFunctionalComponent().ShouldBeFalse();

        ShapeFlags.FunctionalComponent.IsFunctionalComponent().ShouldBeTrue();
        ShapeFlags.FunctionalComponent.IsStatefulComponent().ShouldBeFalse();
    }

    [Fact]
    public void Children_shape_predicates_match_their_bits()
    {
        var element = ShapeFlags.Element | ShapeFlags.ArrayChildren;

        element.HasArrayChildren().ShouldBeTrue();
        element.HasTextChildren().ShouldBeFalse();
        element.HasSlotsChildren().ShouldBeFalse();

        var component = ShapeFlags.StatefulComponent | ShapeFlags.SlotsChildren;

        component.HasSlotsChildren().ShouldBeTrue();
        component.HasTextChildren().ShouldBeFalse();

        (ShapeFlags.Element | ShapeFlags.TextChildren).HasTextChildren().ShouldBeTrue();
    }

    [Fact]
    public void Builtin_predicates_match_teleport_and_suspense()
    {
        ShapeFlags.Teleport.IsTeleport().ShouldBeTrue();
        ShapeFlags.Teleport.IsSuspense().ShouldBeFalse();

        ShapeFlags.Suspense.IsSuspense().ShouldBeTrue();
        ShapeFlags.Suspense.IsTeleport().ShouldBeFalse();

        (ShapeFlags.Suspense | ShapeFlags.ArrayChildren).IsSuspense().ShouldBeTrue();
    }

    [Fact]
    public void KeepAlive_predicates_match_their_bits()
    {
        var shouldKeep = ShapeFlags.StatefulComponent | ShapeFlags.ComponentShouldKeepAlive;

        shouldKeep.ShouldKeepAlive().ShouldBeTrue();
        shouldKeep.IsKeptAlive().ShouldBeFalse();

        var keptAlive = shouldKeep | ShapeFlags.ComponentKeptAlive;

        keptAlive.ShouldKeepAlive().ShouldBeTrue();
        keptAlive.IsKeptAlive().ShouldBeTrue();
        keptAlive.IsComponent().ShouldBeTrue();
    }

    [Fact]
    public void Has_tests_arbitrary_masks_including_composites()
    {
        var flags = ShapeFlags.FunctionalComponent | ShapeFlags.SlotsChildren;

        flags.Has(ShapeFlags.Component).ShouldBeTrue();
        flags.Has(ShapeFlags.SlotsChildren).ShouldBeTrue();
        flags.Has(ShapeFlags.Element).ShouldBeFalse();
        ((ShapeFlags)0).Has(ShapeFlags.Component).ShouldBeFalse();
    }
}
