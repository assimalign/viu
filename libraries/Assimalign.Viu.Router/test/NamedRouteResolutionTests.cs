using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins vue-router's named-route resolution (the name branch of resolve, packages/router/src/
// matcher/index.ts). Reference: https://router.vuejs.org/guide/essentials/named-routes.html
public class NamedRouteResolutionTests
{
    [Fact]
    public void ResolveNamed_InterpolatesParametersIntoFullPath()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var location = matcher.ResolveNamed("user", RouteParameters.Empty.With("id", "42"));

        location.Path.ShouldBe("/users/42");
        location.Name.ShouldBe("user");
        location.Parameters.GetString("id").ShouldBe("42");
        location.IsMatched.ShouldBeTrue();
    }

    [Fact]
    public void ResolveNamed_NestedRoute_InterpolatesAndChainsMatched()
    {
        var child = new RouteRecord(":postId", name: "post");
        var parent = new RouteRecord("/blog", name: "blog", children: [child]);
        var matcher = new RouteMatcher([parent]);

        var location = matcher.ResolveNamed("post", RouteParameters.Empty.With("postId", "7"));

        location.Path.ShouldBe("/blog/7");
        location.Matched.ShouldBe(new[] { parent, child });
    }

    [Fact]
    public void ResolveNamed_OptionalParameterOmitted_ProducesShorterPath()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id?", name: "users")]);

        matcher.ResolveNamed("users").Path.ShouldBe("/users");
        matcher.ResolveNamed("users", RouteParameters.Empty.With("id", "42")).Path.ShouldBe("/users/42");
    }

    [Fact]
    public void ResolveNamed_RepeatableParameter_JoinsValues()
    {
        var matcher = new RouteMatcher([new RouteRecord("/files/:segments+", name: "files")]);

        matcher.ResolveNamed("files", RouteParameters.Empty.WithMany("segments", "a", "b", "c"))
            .Path.ShouldBe("/files/a/b/c");
    }

    [Fact]
    public void ResolveNamed_UnknownName_ThrowsDescriptiveError()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var exception = Should.Throw<RouteMatcherException>(() => matcher.ResolveNamed("does-not-exist"));

        exception.Error.ShouldBe(RouteMatcherError.NamedRouteNotFound);
        exception.Message.ShouldContain("does-not-exist");
    }

    [Fact]
    public void ResolveNamed_MissingRequiredParameter_ThrowsDescriptiveError()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var exception = Should.Throw<RouteMatcherException>(() => matcher.ResolveNamed("user"));

        exception.Error.ShouldBe(RouteMatcherError.MissingRequiredParameter);
        exception.Message.ShouldContain("id");
    }

    [Fact]
    public void HasNamedRoute_ReflectsRegistration()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        matcher.HasNamedRoute("user").ShouldBeTrue();
        matcher.HasNamedRoute("ghost").ShouldBeFalse();
    }

    [Fact]
    public void ResolveNamed_DropsParametersNotDeclaredByTheRoute()
    {
        var matcher = new RouteMatcher([new RouteRecord("/users/:id", name: "user")]);

        var location = matcher.ResolveNamed(
            "user",
            RouteParameters.Empty.With("id", "42").With("unused", "value"));

        location.Parameters.ContainsParameter("id").ShouldBeTrue();
        location.Parameters.ContainsParameter("unused").ShouldBeFalse();
    }
}
