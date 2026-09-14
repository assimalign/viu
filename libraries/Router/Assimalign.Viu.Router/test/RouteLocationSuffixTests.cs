using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// [RTR-12], [V01.01.08.09]: suffix separation consumes the WHATWG URL format
// (https://url.spec.whatwg.org/#url-parsing); Viu matches only the retained path and preserves
// suffix spelling, delimiter presence, and decoded query access as independent contracts.
public sealed class RouteLocationSuffixTests
{
    [Theory]
    [InlineData("/a", "", "", false, false)]
    [InlineData("/a?", "", "", true, false)]
    [InlineData("/a#", "", "", false, true)]
    [InlineData("/a?#", "", "", true, true)]
    [InlineData("/a?x=1&x=2#f", "x=1&x=2", "f", true, true)]
    [InlineData("/a#f?notquery", "", "f?notquery", false, true)]
    [InlineData("/a?x=first?second#f#second", "x=first?second", "f#second", true, true)]
    [InlineData("/a?%78=%26%23%3F+%2B#f%20g", "%78=%26%23%3F+%2B", "f%20g", true, true)]
    [InlineData("/a?=empty&x=&x=two&bare", "=empty&x=&x=two&bare", "", true, false)]
    public void Resolve_SuffixedExactPath_MatchesAndRoundTripsEverySuffixPart(
        string input,
        string rawQuery,
        string fragment,
        bool hasQuery,
        bool hasFragment)
    {
        RouteRecord record = new("/a", name: "page");
        RouteMatcher matcher = new([record]);
        using IRouterHistory history = RouterHistory.CreateMemory("/app/");
        using Router router = new(history, matcher);

        RouteLocation matched = matcher.Resolve(input);
        RouteLocation resolved = router.Resolve(input);

        matched.ShouldBe(resolved);
        resolved.Path.ShouldBe("/a");
        resolved.Name.ShouldBe("page");
        resolved.Matched.ShouldBe([record]);
        resolved.Parameters.ShouldBe(RouteParameters.Empty);
        resolved.RawQuery.ShouldBe(rawQuery);
        resolved.Query.RawText.ShouldBe(rawQuery);
        resolved.Fragment.ShouldBe(fragment);
        resolved.HasQuery.ShouldBe(hasQuery);
        resolved.HasFragment.ShouldBe(hasFragment);
        resolved.FullPath.ShouldBe(input);
        router.CreateHref(resolved).ShouldBe("/app" + input);
        router.CreateHref(input).ShouldBe("/app" + input);
    }

    [Fact]
    public void Resolve_EncodedSeparatorsRemainPathText_WhileQueryAccessDecodesNamesAndValues()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a%3Fb%23c", name: "encoded")]);

        RouteLocation location = router.Resolve("/a%3Fb%23c?%78=%26%23%3F+%2B&x=%E2%9C%93#f%20g");

        location.Path.ShouldBe("/a%3Fb%23c");
        location.Name.ShouldBe("encoded");
        location.Query.TryGetString("x", out string? first).ShouldBeTrue();
        first.ShouldBe("&#? +");
        location.Query.GetStrings("x").ShouldBe(["&#? +", "✓"]);
        location.Fragment.ShouldBe("f%20g");
        router.CreateHref(location).ShouldBe("/a%3Fb%23c?%78=%26%23%3F+%2B&x=%E2%9C%93#f%20g");
    }

    [Fact]
    public void Resolve_EmptyNamesRepeatedValuesAndBareKeys_AreExposedWithoutSuffixInPath()
    {
        RouteMatcher matcher = new([new RouteRecord("/a")]);

        RouteLocation location = matcher.Resolve("/a?=empty&x=&x=two&bare");

        location.Query.GetStrings("").ShouldBe(["empty"]);
        location.Query.GetStrings("x").ShouldBe(["", "two"]);
        location.Query.TryGetString("bare", out string? bare).ShouldBeTrue();
        bare.ShouldBe("");
        location.Query.Names.ShouldBe(["", "x", "bare"]);
    }

    [Fact]
    public void Resolve_UnmatchedSuffixedPath_StillRetainsQueryAndFragment()
    {
        RouteMatcher matcher = new([new RouteRecord("/a")]);

        RouteLocation location = matcher.Resolve("/missing?term=x#details");

        location.IsMatched.ShouldBeFalse();
        location.Path.ShouldBe("/missing");
        location.RawQuery.ShouldBe("term=x");
        location.Query.GetStrings("term").ShouldBe(["x"]);
        location.Fragment.ShouldBe("details");
        location.FullPath.ShouldBe("/missing?term=x#details");
    }

    [Fact]
    public void ResolveNamed_QueryBuilderAndFragment_RoundTripThroughThePathResolver()
    {
        RouteMatcher matcher = new([new RouteRecord("/users/:id", name: "user")]);
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, matcher);
        RouteQuery query = RouteQuery.Empty
            .With("search term", "a+b &?#✓")
            .WithMany("tag", "one", "two");
        RouteParameters parameters = RouteParameters.Empty.With("id", "42");

        RouteLocation resolved = router.ResolveNamed("user", parameters, query, "details?mode=full");
        RouteLocation matched = matcher.ResolveNamed("user", parameters, query, "details?mode=full");
        RouteLocation roundTripped = router.Resolve(router.CreateHref(resolved));

        resolved.ShouldBe(matched);
        resolved.ShouldBe(roundTripped);
        resolved.Path.ShouldBe("/users/42");
        resolved.Parameters.GetString("id").ShouldBe("42");
        resolved.Query.GetStrings("search term").ShouldBe(["a+b &?#✓"]);
        resolved.Query.GetStrings("tag").ShouldBe(["one", "two"]);
        resolved.Fragment.ShouldBe("details?mode=full");
        resolved.FullPath.ShouldBe("/users/42?search+term=a%2Bb+%26%3F%23%E2%9C%93&tag=one&tag=two#details?mode=full");
    }

    [Fact]
    public void ResolveNamed_NullAndEmptySuffixes_PreserveDelimiterPresence()
    {
        RouteMatcher matcher = new([new RouteRecord("/a", name: "page")]);
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, matcher);

        router.ResolveNamed("page", RouteParameters.Empty).FullPath.ShouldBe("/a");
        router.ResolveNamed("page", RouteParameters.Empty, query: null, fragment: "section").FullPath.ShouldBe("/a#section");
        router.ResolveNamed("page", RouteParameters.Empty, query: RouteQuery.Empty, fragment: "").FullPath.ShouldBe("/a?#");
        matcher.ResolveNamed("page", RouteParameters.Empty, query: null, fragment: "section").FullPath.ShouldBe("/a#section");
        matcher.ResolveNamed("page", RouteParameters.Empty, RouteQuery.Empty, "").FullPath.ShouldBe("/a?#");
    }

    [Theory]
    [InlineData("one?item=two")]
    [InlineData("one#section")]
    public void ResolveNamed_RawSuffixDelimiterInPathParameter_RejectsAnAmbiguousLocation(string value)
    {
        // [RTR-12]: path parameters keep their existing encoded-text contract. A raw URL suffix
        // delimiter must be supplied as %3F or %23 so CreateHref cannot reinterpret it as a suffix.
        RouteMatcher matcher = new([new RouteRecord("/users/:id", name: "user")]);

        ArgumentException exception = Should.Throw<ArgumentException>(
            () => matcher.ResolveNamed("user", RouteParameters.Empty.With("id", value)));

        exception.ParamName.ShouldBe("parameters");
    }

    [Fact]
    public void ResolveNamed_EncodedSuffixDelimitersInPathParameters_RoundTripWithoutChangingParameterSpelling()
    {
        RouteMatcher matcher = new([new RouteRecord("/users/:id", name: "user")]);
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, matcher);

        RouteLocation location = router.ResolveNamed(
            "user",
            RouteParameters.Empty.With("id", "one%3Ftwo%23three"),
            RouteQuery.Parse("item=four"),
            "section");

        location.Path.ShouldBe("/users/one%3Ftwo%23three");
        location.Parameters.GetString("id").ShouldBe("one%3Ftwo%23three");
        location.FullPath.ShouldBe("/users/one%3Ftwo%23three?item=four#section");
        router.Resolve(router.CreateHref(location)).ShouldBe(location);
    }

    [Theory]
    [InlineData("/a?x=1#first", "/a?x=2#first")]
    [InlineData("/a?x=1#first", "/a?x=1#second")]
    [InlineData("/a", "/a?")]
    [InlineData("/a", "/a#")]
    [InlineData("/a?x=+", "/a?x=%20")]
    [InlineData("/a?x=1&y=2", "/a?y=2&x=1")]
    public void Equality_SuffixValueSpellingOrderOrPresenceDiffers_LocationsAreDistinct(
        string firstInput,
        string secondInput)
    {
        RouteMatcher matcher = new([new RouteRecord("/a")]);
        RouteLocation first = matcher.Resolve(firstInput);
        RouteLocation second = matcher.Resolve(secondInput);
        RouteLocation equalFirst = matcher.Resolve(firstInput);

        (first == second).ShouldBeFalse();
        (first != second).ShouldBeTrue();
        first.ShouldBe(equalFirst);
        first.GetHashCode().ShouldBe(equalFirst.GetHashCode());
        HashSet<RouteLocation> values = [first, second, equalFirst];
        values.Count.ShouldBe(2);
    }

    [Fact]
    public void Start_HasNoQueryOrFragmentAndRetainsTheInitialSentinelContract()
    {
        RouteLocation.Start.Path.ShouldBe("/");
        RouteLocation.Start.FullPath.ShouldBe("/");
        RouteLocation.Start.Query.ShouldBe(RouteQuery.Empty);
        RouteLocation.Start.RawQuery.ShouldBeEmpty();
        RouteLocation.Start.Fragment.ShouldBeEmpty();
        RouteLocation.Start.HasQuery.ShouldBeFalse();
        RouteLocation.Start.HasFragment.ShouldBeFalse();
        RouteLocation.Start.Matched.ShouldBeEmpty();
    }
}
