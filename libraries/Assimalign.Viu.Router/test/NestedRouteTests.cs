using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins vue-router's nested-route resolution and route.matched semantics.
// Reference: https://router.vuejs.org/guide/essentials/nested-routes.html
public class NestedRouteTests
{
    [Fact]
    public void Resolve_NestedRoute_BuildsParentToChildMatchedChain()
    {
        var child = new RouteRecord(":id", name: "user");
        var parent = new RouteRecord("/users", name: "users", children: [child]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/users/42");

        location.Matched.Count.ShouldBe(2);
        location.Matched[0].ShouldBeSameAs(parent);
        location.Matched[1].ShouldBeSameAs(child);
        location.Name.ShouldBe("user");
        location.Parameters.GetString("id").ShouldBe("42");
    }

    [Fact]
    public void Resolve_ChildPath_JoinsOntoParentPath()
    {
        var child = new RouteRecord("settings", name: "user-settings");
        var parent = new RouteRecord("/users/:id", children: [child]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/users/42/settings");

        location.Name.ShouldBe("user-settings");
        location.Parameters.GetString("id").ShouldBe("42");
        location.Matched.Select(record => record).ShouldBe(new[] { parent, child });
    }

    [Fact]
    public void Resolve_EmptyPathChild_ResolvesWhenNavigatingToParentPath()
    {
        // The empty-path default child must win over the bare parent path, yielding a two-entry
        // matched chain — the classic layout-with-default-view case.
        var defaultChild = new RouteRecord(string.Empty, name: "dashboard-home");
        var parent = new RouteRecord("/dashboard", name: "dashboard", children: [defaultChild]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/dashboard");

        location.Matched.Count.ShouldBe(2);
        location.Matched[0].ShouldBeSameAs(parent);
        location.Matched[1].ShouldBeSameAs(defaultChild);
        location.Name.ShouldBe("dashboard-home");
    }

    [Fact]
    public void Resolve_DeeplyNestedRoute_ChainsEveryAncestor()
    {
        var grandchild = new RouteRecord("edit", name: "post-edit");
        var child = new RouteRecord(":postId", name: "post", children: [grandchild]);
        var parent = new RouteRecord("/blog", name: "blog", children: [child]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/blog/7/edit");

        location.Matched.ShouldBe(new[] { parent, child, grandchild });
        location.Name.ShouldBe("post-edit");
        location.Parameters.GetInteger("postId").ShouldBe(7);
    }

    [Fact]
    public void Resolve_AbsoluteChildPath_DoesNotJoinParent()
    {
        // A child path starting with '/' is absolute and is not joined onto the parent.
        var absoluteChild = new RouteRecord("/top-level", name: "top");
        var parent = new RouteRecord("/parent", children: [absoluteChild]);
        var matcher = new RouteMatcher([parent]);

        matcher.Resolve("/top-level").Name.ShouldBe("top");
        matcher.Resolve("/parent/top-level").IsMatched.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_ParentWithNonEmptyChildOnly_MatchesParentAlone()
    {
        var child = new RouteRecord("details", name: "details");
        var parent = new RouteRecord("/item", name: "item", children: [child]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.Resolve("/item");

        location.Matched.ShouldBe(new[] { parent });
        location.Name.ShouldBe("item");
    }
}
