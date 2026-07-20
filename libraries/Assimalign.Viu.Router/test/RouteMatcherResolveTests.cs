using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// General resolution behavior of the matcher facade (the C# port of createRouterMatcher's resolve,
// packages/router/src/matcher/index.ts) plus the ticket's dependency-boundary guarantee.
public class RouteMatcherResolveTests
{
    [Fact]
    public void Resolve_UnmatchedPath_ReturnsEmptyMatchedWithoutThrowing()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var location = matcher.Resolve("/no/such/path");

        location.IsMatched.ShouldBeFalse();
        location.Matched.ShouldBeEmpty();
        location.Name.ShouldBeNull();
        location.Path.ShouldBe("/no/such/path");
    }

    [Fact]
    public void AddRoute_AfterConstruction_ExtendsTheTable()
    {
        var matcher = new RouteMatcher();

        matcher.AddRoute(new RouteRecord("/about", name: "about"));

        matcher.Resolve("/about").Name.ShouldBe("about");
    }

    [Fact]
    public void Resolve_MergesMetaAcrossMatchedChain_ChildOverridesParent()
    {
        var child = new RouteRecord(
            ":id",
            name: "user",
            meta: new Dictionary<string, object?> { ["title"] = "User", ["layout"] = "user" });
        var parent = new RouteRecord(
            "/users",
            name: "users",
            children: [child],
            meta: new Dictionary<string, object?> { ["layout"] = "admin" });
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/users/42");

        location.Meta["title"].ShouldBe("User");
        location.Meta["layout"].ShouldBe("user");
    }

    [Fact]
    public void Resolve_SameLocation_ProducesValueEqualResults()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var first = matcher.Resolve("/users/42");
        var second = matcher.Resolve("/users/42");

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Resolve_DifferentParameters_AreNotEqual()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        matcher.Resolve("/users/42").ShouldNotBe(matcher.Resolve("/users/7"));
    }

    [Fact]
    public void GetRoutes_ReturnsEveryRegisteredRecord()
    {
        var child = new RouteRecord(":id", name: "user");
        var parent = new RouteRecord("/users", name: "users", children: [child]);
        var matcher = new RouteMatcher([parent, new RouteRecord("/about", name: "about")]);

        var routes = matcher.GetRoutes();

        routes.ShouldContain(parent);
        routes.ShouldContain(child);
        routes.Count.ShouldBe(3);
    }

    [Fact]
    public void Resolve_RootPath_MatchesRootRecord()
    {
        var matcher = new RouteMatcher([new RouteRecord("/", name: "home")]);

        matcher.Resolve("/").Name.ShouldBe("home");
    }

    [Fact]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Test-only reflection over assembly references to assert the matcher's dependency boundary; the test project is never trimmed or AOT-published.")]
    public void RouterAssembly_DependsOnNoOtherViuLibrary()
    {
        // Acceptance criterion (matcher + memory history stay runnable in a plain .NET host): this
        // assembly references no other Viu library and no Viu renderer/interop assembly. [V01.01.08.02]
        // added a browser history edge over the framework's System.Runtime.InteropServices.JavaScript
        // primitive (gated by [SupportedOSPlatform("browser")], exercised only through the injected
        // seam off-browser); that framework reference is allowed — the forbidden coupling is to
        // another Viu package, which would drag the DOM bridge / renderer in and break the pure host.
        var referenced = typeof(RouteMatcher).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldNotContain(name => name.StartsWith("Assimalign.", StringComparison.Ordinal));
    }
}
