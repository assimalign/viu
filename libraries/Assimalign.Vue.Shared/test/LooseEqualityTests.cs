using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

// Pins Vue's loose equality (looseEqual.ts, vectors from vuejs/core's looseEqual.spec.ts) —
// the semantics behind checkbox/select v-model value matching.
public class LooseEqualityTests
{
    [Fact]
    public void LooseEqual_StrictlyEqualValues()
    {
        LooseEquality.LooseEqual(1, 1).ShouldBeTrue();
        LooseEquality.LooseEqual("a", "a").ShouldBeTrue();
        LooseEquality.LooseEqual(null, null).ShouldBeTrue();
        LooseEquality.LooseEqual(true, true).ShouldBeTrue();
    }

    [Fact]
    public void LooseEqual_CoercesNumberAndStringLikeJavaScript()
    {
        // Upstream fallback: String(a) === String(b).
        LooseEquality.LooseEqual(1, "1").ShouldBeTrue();
        LooseEquality.LooseEqual(1.5, "1.5").ShouldBeTrue();
        LooseEquality.LooseEqual(1, "2").ShouldBeFalse();
        LooseEquality.LooseEqual(true, "true").ShouldBeTrue();
    }

    [Fact]
    public void LooseEqual_NullAgainstAnythingElse_IsFalse()
    {
        LooseEquality.LooseEqual(null, 0).ShouldBeFalse();
        LooseEquality.LooseEqual("", null).ShouldBeFalse();
    }

    [Fact]
    public void LooseEqual_ArraysCompareElementWise()
    {
        LooseEquality.LooseEqual(new object?[] { 1, 2 }, new object?[] { 1, 2 }).ShouldBeTrue();
        LooseEquality.LooseEqual(new object?[] { 1, 2 }, new object?[] { "1", "2" }).ShouldBeTrue();
        LooseEquality.LooseEqual(new object?[] { 1, 2 }, new object?[] { 1 }).ShouldBeFalse();
        LooseEquality.LooseEqual(new object?[] { 1 }, 1).ShouldBeFalse();
        LooseEquality.LooseEqual(
            new object?[] { new object?[] { 1 } },
            new object?[] { new object?[] { "1" } }).ShouldBeTrue();
    }

    [Fact]
    public void LooseEqual_DictionariesCompareKeyWise()
    {
        LooseEquality.LooseEqual(
            new Dictionary<string, object?> { ["a"] = 1 },
            new Dictionary<string, object?> { ["a"] = "1" }).ShouldBeTrue();
        LooseEquality.LooseEqual(
            new Dictionary<string, object?> { ["a"] = 1 },
            new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }).ShouldBeFalse();
        LooseEquality.LooseEqual(
            new Dictionary<string, object?> { ["a"] = 1 },
            new Dictionary<string, object?> { ["b"] = 1 }).ShouldBeFalse();
    }

    [Fact]
    public void LooseEqual_DatesCompareByInstant()
    {
        var moment = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
        LooseEquality.LooseEqual(moment, new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc)).ShouldBeTrue();
        LooseEquality.LooseEqual(moment, moment.AddTicks(1)).ShouldBeFalse();
        LooseEquality.LooseEqual(moment, "not a date").ShouldBeFalse();
    }

    [Fact]
    public void LooseIndexOf_MatchesLoosely()
    {
        var values = new object?[] { "1", "2", "3" };
        LooseEquality.LooseIndexOf(values, 2).ShouldBe(1);
        LooseEquality.LooseIndexOf(values, "3").ShouldBe(2);
        LooseEquality.LooseIndexOf(values, 4).ShouldBe(-1);
    }
}
