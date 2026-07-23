using System.Threading.Tasks;

using Shouldly;
using Xunit;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins RouterLink against vue-router's <RouterLink> (packages/router/src/RouterLink.ts,
// https://router.vuejs.org/api/#Component-RouterLink): href resolved through the router (base
// included), active/exact-active classes by route matching
// (https://router.vuejs.org/guide/essentials/active-links.html), and the guardEvent click
// conditions (only an unmodified, primary-button, un-prevented, non-_blank click navigates). All
// DOM-free through the Testing renderer with memory history.
public class RouterLinkTests
{
    private static Router LinkRouter(string? basePath = null)
        => new(
            RouterHistory.CreateMemory(basePath),
            [
                new RouteRecord("/", name: "home"),
                new RouteRecord("/users", children:
                [
                    new RouteRecord(":id"),
                ]),
            ]);

    [Fact]
    public void RouterLink_ResolvesHref_ThroughTheRouter()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        wrapper.Get("a").Attribute("href").ShouldBe("/users/1");
        wrapper.Text().ShouldBe("User 1");
    }

    [Fact]
    public void RouterLink_IncludesBasePath_InHref()
    {
        var router = LinkRouter("/app");
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        wrapper.Get("a").Attribute("href").ShouldBe("/app/users/1");
    }

    [Fact]
    public void RouterLink_AppliesActiveClass_OnInclusivePrefixMatch()
    {
        var router = LinkRouter();
        _ = router.Push("/users/1");
        using var wrapper = MountLink(router, Arguments(("to", "/users")), TextSlot("Users"));

        // /users is an ancestor of the current /users/1 -> active, but not the exact current route.
        (wrapper.Get("a").Attribute("class") as string).ShouldBe("router-link-active");
    }

    [Fact]
    public void RouterLink_AppliesExactActiveClass_OnExactMatch()
    {
        var router = LinkRouter();
        _ = router.Push("/users/1");
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        (wrapper.Get("a").Attribute("class") as string).ShouldBe("router-link-active router-link-exact-active");
    }

    [Fact]
    public void RouterLink_AppliesNoActiveClass_WhenNotMatched()
    {
        var router = LinkRouter();
        _ = router.Push("/users/1");
        using var wrapper = MountLink(router, Arguments(("to", "/")), TextSlot("Home"));

        wrapper.Get("a").Attribute("class").ShouldBeNull();
    }

    [Fact]
    public void RouterLink_UsesGloballyConfiguredActiveClasses()
    {
        var router = LinkRouter();
        router.LinkActiveClass = "is-active";
        router.LinkExactActiveClass = "is-exact";
        _ = router.Push("/users/1");
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        (wrapper.Get("a").Attribute("class") as string).ShouldBe("is-active is-exact");
    }

    [Fact]
    public void RouterLink_PerLinkClassProps_OverrideTheGlobalDefaults()
    {
        var router = LinkRouter();
        _ = router.Push("/users/1");
        var properties = Arguments(
            ("to", "/users/1"),
            ("activeClass", "link-on"),
            ("exactActiveClass", "link-exact"));
        using var wrapper = MountLink(router, properties, TextSlot("User 1"));

        (wrapper.Get("a").Attribute("class") as string).ShouldBe("link-on link-exact");
    }

    [Fact]
    public async Task RouterLink_ActiveClass_UpdatesReactivelyOnNavigation()
    {
        var router = LinkRouter();
        _ = router.Push("/users/1");
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));
        (wrapper.Get("a").Attribute("class") as string).ShouldBe("router-link-active router-link-exact-active");

        // A different param than the link's target: the reactive route drives a re-render that drops
        // the active classes.
        _ = router.Push("/users/2");
        await wrapper.NextTickAsync();

        wrapper.Find(".router-link-active").ShouldBeNull();
    }

    [Fact]
    public async Task RouterLink_LeftClick_NavigatesClientSideAndPreventsDefault()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));
        router.CurrentRoute.Value.Path.ShouldBe("/");

        var click = new RouterLinkClickEvent();
        await wrapper.Trigger("click", click);

        router.CurrentRoute.Value.Path.ShouldBe("/users/1");
        click.DefaultPrevented.ShouldBeTrue();
    }

    [Fact]
    public async Task RouterLink_ModifierClick_FallsThroughToTheBrowser()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        var click = new RouterLinkClickEvent(controlKey: true);
        await wrapper.Trigger("click", click);

        router.CurrentRoute.Value.Path.ShouldBe("/");
        click.DefaultPrevented.ShouldBeFalse();
    }

    [Fact]
    public async Task RouterLink_MiddleClick_FallsThroughToTheBrowser()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        await wrapper.Trigger("click", new RouterLinkClickEvent(button: 1));

        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task RouterLink_RightClick_FallsThroughToTheBrowser()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        await wrapper.Trigger("click", new RouterLinkClickEvent(button: 2));

        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task RouterLink_AlreadyPreventedClick_DoesNotNavigate()
    {
        var router = LinkRouter();
        using var wrapper = MountLink(router, Arguments(("to", "/users/1")), TextSlot("User 1"));

        var click = new RouterLinkClickEvent();
        click.PreventDefault();
        await wrapper.Trigger("click", click);

        router.CurrentRoute.Value.Path.ShouldBe("/");
    }

    [Fact]
    public async Task RouterLink_TargetBlank_FallsThroughToTheBrowser()
    {
        var router = LinkRouter();
        var properties = Arguments(("to", "/users/1"), ("target", "_blank"));
        using var wrapper = MountLink(router, properties, TextSlot("User 1"));

        var click = new RouterLinkClickEvent();
        await wrapper.Trigger("click", click);

        router.CurrentRoute.Value.Path.ShouldBe("/");
        click.DefaultPrevented.ShouldBeFalse();
    }

    [Fact]
    public async Task RouterLink_ReplaceProp_NavigatesByReplacingTheHistoryEntry()
    {
        var history = RouterHistory.CreateMemory();
        var router = new Router(
            history,
            [
                new RouteRecord("/", name: "home"),
                new RouteRecord("/users", children:
                [
                    new RouteRecord(":id"),
                ]),
            ]);
        var properties = Arguments(("to", "/users/1"), ("replace", true));
        using var wrapper = MountLink(router, properties, TextSlot("User 1"));

        await wrapper.Trigger("click", new RouterLinkClickEvent());

        router.CurrentRoute.Value.Path.ShouldBe("/users/1");
        history.State.Replaced.ShouldBeTrue();
    }
}
