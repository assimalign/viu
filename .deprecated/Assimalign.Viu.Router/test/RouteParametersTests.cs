using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Typed, boxing-free, reflection-free parameter accessors — the C# counterpart of vue-router's
// RouteParams (packages/router/src/matcher/pathParserRanker.ts, PathParams).
public class RouteParametersTests
{
    [Fact]
    public void With_IsImmutable_AndDoesNotMutateTheSource()
    {
        var original = RouteParameters.Empty;
        var extended = original.With("id", "42");

        original.Count.ShouldBe(0);
        extended.Count.ShouldBe(1);
        extended.GetString("id").ShouldBe("42");
    }

    [Fact]
    public void GetInteger_ParsesInvariantInteger()
    {
        RouteParameters.Empty.With("id", "42").GetInteger("id").ShouldBe(42);
    }

    [Fact]
    public void GetInteger_NonInteger_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => RouteParameters.Empty.With("id", "abc").GetInteger("id"));
    }

    [Fact]
    public void GetString_MissingParameter_ThrowsKeyNotFound()
    {
        Should.Throw<KeyNotFoundException>(() => RouteParameters.Empty.GetString("missing"));
    }

    [Fact]
    public void TryGetString_And_TryGetInteger_ReportPresence()
    {
        var parameters = RouteParameters.Empty.With("id", "7");

        parameters.TryGetString("id", out var text).ShouldBeTrue();
        text.ShouldBe("7");
        parameters.TryGetInteger("id", out var number).ShouldBeTrue();
        number.ShouldBe(7);

        parameters.TryGetString("missing", out _).ShouldBeFalse();
        parameters.TryGetInteger("missing", out var fallback).ShouldBeFalse();
        fallback.ShouldBe(0);
    }

    [Fact]
    public void GetStrings_ReturnsRepeatableValues()
    {
        var parameters = RouteParameters.Empty.WithMany("chapters", "one", "two");

        parameters.GetStrings("chapters").ShouldBe(new[] { "one", "two" });
    }

    [Fact]
    public void GetStrings_SingleValue_ReturnsOneElement()
    {
        RouteParameters.Empty.With("id", "42").GetStrings("id").ShouldBe(new[] { "42" });
    }

    [Fact]
    public void GetStrings_MissingParameter_ReturnsEmpty()
    {
        RouteParameters.Empty.GetStrings("missing").ShouldBeEmpty();
    }

    [Fact]
    public void Equality_IsValueBased_AndOrderIndependent()
    {
        var left = RouteParameters.Empty.With("a", "1").With("b", "2");
        var right = RouteParameters.Empty.With("b", "2").With("a", "1");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesSingleFromRepeatable()
    {
        var single = RouteParameters.Empty.With("id", "42");
        var repeatable = RouteParameters.Empty.WithMany("id", "42");

        single.ShouldNotBe(repeatable);
    }

    [Fact]
    public void Names_ReportsEveryParameter()
    {
        var parameters = RouteParameters.Empty.With("a", "1").With("b", "2");

        parameters.Names.ShouldBe(new[] { "a", "b" }, ignoreOrder: true);
    }
}
