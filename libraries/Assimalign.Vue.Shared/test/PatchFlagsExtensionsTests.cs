using Assimalign.Vue.Shared;
using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

public class PatchFlagsExtensionsTests
{
    [Theory]
    [InlineData(PatchFlags.Text)]
    [InlineData(PatchFlags.Class)]
    [InlineData(PatchFlags.Style)]
    [InlineData(PatchFlags.Props)]
    [InlineData(PatchFlags.FullProps)]
    [InlineData(PatchFlags.NeedHydration)]
    [InlineData(PatchFlags.StableFragment)]
    [InlineData(PatchFlags.KeyedFragment)]
    [InlineData(PatchFlags.UnkeyedFragment)]
    [InlineData(PatchFlags.NeedPatch)]
    [InlineData(PatchFlags.DynamicSlots)]
    [InlineData(PatchFlags.DevRootFragment)]
    public void Bail_never_satisfies_a_positive_flag_check(PatchFlags positive)
    {
        // -2 in two's complement has every bit except bit 0 set, so an unguarded bitwise test
        // would spuriously match most flags. The guarded helper must always say no.
        PatchFlags.Bail.Has(positive).ShouldBeFalse();
    }

    [Theory]
    [InlineData(PatchFlags.Text)]
    [InlineData(PatchFlags.Class)]
    [InlineData(PatchFlags.Style)]
    [InlineData(PatchFlags.Props)]
    [InlineData(PatchFlags.FullProps)]
    [InlineData(PatchFlags.NeedHydration)]
    [InlineData(PatchFlags.StableFragment)]
    [InlineData(PatchFlags.KeyedFragment)]
    [InlineData(PatchFlags.UnkeyedFragment)]
    [InlineData(PatchFlags.NeedPatch)]
    [InlineData(PatchFlags.DynamicSlots)]
    [InlineData(PatchFlags.DevRootFragment)]
    public void Cached_never_satisfies_a_positive_flag_check(PatchFlags positive)
    {
        // -1 has every bit set, so every unguarded bitwise test would match. The guarded
        // helper must always say no.
        PatchFlags.Cached.Has(positive).ShouldBeFalse();
    }

    [Fact]
    public void Bail_fails_every_named_positive_predicate()
    {
        var flags = PatchFlags.Bail;

        flags.HasText().ShouldBeFalse();
        flags.HasDynamicClass().ShouldBeFalse();
        flags.HasDynamicStyle().ShouldBeFalse();
        flags.HasDynamicProps().ShouldBeFalse();
        flags.HasFullProps().ShouldBeFalse();
        flags.NeedsHydration().ShouldBeFalse();
        flags.IsStableFragment().ShouldBeFalse();
        flags.IsKeyedFragment().ShouldBeFalse();
        flags.IsUnkeyedFragment().ShouldBeFalse();
        flags.NeedsPatch().ShouldBeFalse();
        flags.HasDynamicSlots().ShouldBeFalse();
        flags.IsDevRootFragment().ShouldBeFalse();

        flags.IsBail().ShouldBeTrue();
        flags.IsCached().ShouldBeFalse();
    }

    [Fact]
    public void Cached_round_trips_as_minus_one_and_only_matches_the_cached_predicate()
    {
        var flags = (PatchFlags)(-1);

        flags.ShouldBe(PatchFlags.Cached);
        ((int)flags).ShouldBe(-1);
        flags.IsCached().ShouldBeTrue();
        flags.IsBail().ShouldBeFalse();
        flags.HasText().ShouldBeFalse();
        flags.HasDynamicSlots().ShouldBeFalse();
    }

    [Fact]
    public void Named_predicates_match_their_own_flag_and_nothing_else()
    {
        PatchFlags.Text.HasText().ShouldBeTrue();
        PatchFlags.Class.HasDynamicClass().ShouldBeTrue();
        PatchFlags.Style.HasDynamicStyle().ShouldBeTrue();
        PatchFlags.Props.HasDynamicProps().ShouldBeTrue();
        PatchFlags.FullProps.HasFullProps().ShouldBeTrue();
        PatchFlags.NeedHydration.NeedsHydration().ShouldBeTrue();
        PatchFlags.StableFragment.IsStableFragment().ShouldBeTrue();
        PatchFlags.KeyedFragment.IsKeyedFragment().ShouldBeTrue();
        PatchFlags.UnkeyedFragment.IsUnkeyedFragment().ShouldBeTrue();
        PatchFlags.NeedPatch.NeedsPatch().ShouldBeTrue();
        PatchFlags.DynamicSlots.HasDynamicSlots().ShouldBeTrue();
        PatchFlags.DevRootFragment.IsDevRootFragment().ShouldBeTrue();

        PatchFlags.Text.HasDynamicClass().ShouldBeFalse();
        PatchFlags.Class.HasText().ShouldBeFalse();
        PatchFlags.StableFragment.IsKeyedFragment().ShouldBeFalse();
    }

    [Fact]
    public void Predicates_work_on_combined_flags()
    {
        var flags = PatchFlags.Text | PatchFlags.Class | PatchFlags.NeedHydration;

        flags.HasText().ShouldBeTrue();
        flags.HasDynamicClass().ShouldBeTrue();
        flags.NeedsHydration().ShouldBeTrue();
        flags.HasDynamicStyle().ShouldBeFalse();
        flags.HasDynamicProps().ShouldBeFalse();
        flags.IsCached().ShouldBeFalse();
        flags.IsBail().ShouldBeFalse();

        flags.Has(PatchFlags.Text).ShouldBeTrue();
        flags.Has(PatchFlags.Text | PatchFlags.Style).ShouldBeTrue();
        flags.Has(PatchFlags.Style).ShouldBeFalse();
    }

    [Fact]
    public void Sentinel_arguments_never_satisfy_a_positive_receiver()
    {
        // Cached (-1) has every bit set and Bail (-2) all but bit 0, so an unguarded ARGUMENT
        // would make any positive receiver match. The guarded helper must always say no;
        // sentinels are checked with IsCached()/IsBail() instead.
        PatchFlags.Text.Has(PatchFlags.Cached).ShouldBeFalse();
        PatchFlags.Text.Has(PatchFlags.Bail).ShouldBeFalse();
        (PatchFlags.Text | PatchFlags.Class).Has(PatchFlags.Cached).ShouldBeFalse();
        (PatchFlags.Text | PatchFlags.Class).Has(PatchFlags.Bail).ShouldBeFalse();
        PatchFlags.Text.Has((PatchFlags)0).ShouldBeFalse();
    }

    [Fact]
    public void Zero_flags_satisfy_no_predicate()
    {
        var flags = (PatchFlags)0;

        flags.Has(PatchFlags.Text).ShouldBeFalse();
        flags.HasText().ShouldBeFalse();
        flags.IsCached().ShouldBeFalse();
        flags.IsBail().ShouldBeFalse();
    }
}
