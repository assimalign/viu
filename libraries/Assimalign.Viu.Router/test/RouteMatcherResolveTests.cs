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
        Justification = "Test-only reflection over assembly references to assert the Router dependency boundary; the test project is never trimmed or AOT-published.")]
    public void RouterAssembly_DoesNotReferenceTheBrowserDomAdapter()
    {
        // [V01.01.08.03] (issue #72) places RouterView/RouterLink in this assembly, so it now
        // references Assimalign.Viu.RuntimeCore and Assimalign.Viu.Reactivity — a deliberate,
        // documented relaxation of the [V01.01.08.01]/[V01.01.08.02] "no other Viu library"
        // assertion. Deviates from that prior boundary per issue #72's stated architecture (the
        // Router area may reference Runtime Core / Reactivity). The matcher and memory-history code
        // still uses neither reference, so it stays runnable in a plain .NET host; the forbidden
        // coupling is now the browser DOM adapter (Assimalign.Viu.RuntimeDom), because the components
        // must render through the injected node-ops abstraction to work against the in-memory test
        // renderer and the SSR renderer, never the DOM directly. The framework's
        // System.Runtime.InteropServices.JavaScript reference from the [V01.01.08.02] browser history
        // edge stays allowed (gated by [SupportedOSPlatform("browser")]).
        var referenced = typeof(RouterView).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldNotContain("Assimalign.Viu.RuntimeDom");
        // Positive check: the component-model wiring the components depend on is actually in place.
        referenced.ShouldContain("Assimalign.Viu.RuntimeCore");
    }
}
