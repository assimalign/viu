using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Forms.Tests;

// Pins the reactive form model: the derived IsValid/Summary computeds and, where dependency-tracking
// bugs hide, their *run counts*. IsValid's && short-circuit is a deliberate lazy-tracking assertion
// (Vue's computed only tracks what it actually reads). Upstream: https://vuejs.org/guide/essentials/forms.html
public sealed class RegistrationFormTests
{
    [Fact]
    public void IsValid_TracksLazily_ShortCircuitingOnTheFirstFalseTerm()
    {
        var form = new RegistrationForm();
        var runs = 0;
        var valid = true;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            valid = form.IsValid.Value;
        });

        runs.ShouldBe(1);
        valid.ShouldBeFalse();

        // FullName is empty, so `&&` short-circuits after the first term: Email and AcceptsTerms are
        // not read this pass, so changing them does not recompute IsValid (Vue's lazy tracking).
        form.Email.Value = "ada@example.com";
        form.AcceptsTerms.Value = true;
        runs.ShouldBe(1);
        valid.ShouldBeFalse();

        // Changing the one tracked term recomputes, now reading all three terms → valid.
        form.FullName.Value = "Ada Lovelace";
        runs.ShouldBe(2);
        valid.ShouldBeTrue();

        effect.Stop();
    }

    [Fact]
    public void Summary_RecomputesOnAnyFieldChange_AndCoalescesEqualWrites()
    {
        var form = new RegistrationForm();
        var runs = 0;
        var summary = string.Empty;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            summary = form.Summary.Value;
        });

        runs.ShouldBe(1);
        summary.ShouldContain("(no name)");

        form.FullName.Value = "Ada";
        runs.ShouldBe(2);
        summary.ShouldContain("Ada");

        // Equal-value write does not trigger, so the computed does not recompute.
        form.FullName.Value = "Ada";
        runs.ShouldBe(2);

        form.AcceptsTerms.Value = true;
        runs.ShouldBe(3);
        summary.ShouldContain("terms accepted");

        effect.Stop();
    }

    [Fact]
    public void Summary_ReflectsInterestListReplacement()
    {
        var form = new RegistrationForm();

        form.Interests.Value = new List<string> { "Reactivity", "WASM" };

        form.Summary.Value.ShouldContain("interests [Reactivity, WASM]");
    }

    [Fact]
    public void Age_HoldsANumber_ThroughItsReference()
    {
        var form = new RegistrationForm();

        form.Age.Value = 36d;

        form.Age.Value.ShouldBe(36d);
        form.Summary.Value.ShouldContain("age 36");
    }
}
