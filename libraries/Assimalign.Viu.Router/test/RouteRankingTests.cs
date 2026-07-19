using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins vue-router's ranking model (comparePathParserScore, packages/router/src/matcher/
// pathParserRanker.ts): static beats dynamic beats catch-all, independent of table order.
// Reference: https://router.vuejs.org/guide/essentials/route-matching-syntax.html
public class RouteRankingTests
{
    private static IReadOnlyList<string> OrderedPaths(RouteMatcher matcher)
        => matcher.Matchers.Select(entry => entry.NormalizedPath).ToArray();

    [Fact]
    public void Resolve_StaticSegment_OutranksDynamicSegment()
    {
        // Dynamic added first — specificity, not order, must decide.
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/users/:id", name: "user"),
            new RouteRecord("/users/new", name: "new-user"),
        ]);

        matcher.Resolve("/users/new").Name.ShouldBe("new-user");
        matcher.Resolve("/users/42").Name.ShouldBe("user");
    }

    [Fact]
    public void Resolve_DynamicSegment_OutranksCatchAllWildcard()
    {
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/:pathMatch(.*)*", name: "not-found"),
            new RouteRecord("/users/:id", name: "user"),
        ]);

        matcher.Resolve("/users/42").Name.ShouldBe("user");
        matcher.Resolve("/something/else").Name.ShouldBe("not-found");
    }

    [Fact]
    public void Resolve_TableOrder_DoesNotOverrideSpecificity()
    {
        // Deliberately register least-specific first.
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/:pathMatch(.*)*", name: "not-found"),
            new RouteRecord("/users/:id", name: "user"),
            new RouteRecord("/users/new", name: "new-user"),
        ]);

        matcher.Resolve("/users/new").Name.ShouldBe("new-user");
        matcher.Resolve("/users/42").Name.ShouldBe("user");
        matcher.Resolve("/anything").Name.ShouldBe("not-found");
    }

    [Fact]
    public void Matchers_AreOrderedByDescendingSpecificity()
    {
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/:pathMatch(.*)*"),
            new RouteRecord("/users/:id"),
            new RouteRecord("/users/new"),
        ]);

        // Static-most-specific first; catch-all last, regardless of insertion order.
        OrderedPaths(matcher).ShouldBe(new[] { "/users/new", "/users/:id", "/:pathMatch(.*)*" });
    }

    [Fact]
    public void Resolve_CustomPattern_OutranksBareDynamicParameter()
    {
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/users/:id", name: "user"),
            new RouteRecord(@"/users/:id(\d+)", name: "numeric-user"),
        ]);

        // The numeric constraint is more specific, so it wins when it matches...
        matcher.Resolve("/users/42").Name.ShouldBe("numeric-user");
        // ...and falls through to the bare parameter when it does not.
        matcher.Resolve("/users/abc").Name.ShouldBe("user");
    }

    [Fact]
    public void Resolve_RootAndStaticSegments_RankAboveDynamicRoot()
    {
        var matcher = new RouteMatcher(
        [
            new RouteRecord("/:slug"),
            new RouteRecord("/about", name: "about"),
        ]);

        matcher.Resolve("/about").Name.ShouldBe("about");
        matcher.Resolve("/anything-else").Name.ShouldBeNull();
        matcher.Resolve("/anything-else").IsMatched.ShouldBeTrue();
    }
}
