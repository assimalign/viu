using System.Threading.Tasks;

using Assimalign.Viu.HackerNews;
using Assimalign.Viu.Router;

using Shouldly;

using Xunit;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Viu.HackerNews.Tests;

/// <summary>
/// Covers the sample's route table resolution and the root-redirect guard against a memory-history
/// router (no browser). The route table is plain Viu code, so it is unit-testable directly.
/// </summary>
public sealed class RouteTableTests
{
    private static ViuRouter CreateRouter()
    {
        var router = new ViuRouter(RouterHistory.CreateMemory(), AppRoutes.Create());
        router.BeforeEach(AppRoutes.RedirectRoot);
        return router;
    }

    [Theory]
    [InlineData("/top", "top", 0)]
    [InlineData("/new", "new", 0)]
    [InlineData("/show/3", "show", 3)]
    [InlineData("/ask/2", "ask", 2)]
    [InlineData("/jobs", "jobs", 0)]
    public void Feed_routes_resolve_feed_and_page(string path, string expectedFeed, int expectedPage)
    {
        var route = CreateRouter().Resolve(path);

        route.Name.ShouldBe("feed");
        route.Parameters.GetString("feed").ShouldBe(expectedFeed);
        route.Parameters.TryGetInteger("page", out var page);
        (page).ShouldBe(expectedPage == 0 ? 0 : expectedPage);
    }

    [Fact]
    public void Item_route_resolves_numeric_id()
    {
        var route = CreateRouter().Resolve("/item/8863");

        route.Name.ShouldBe("item");
        route.Parameters.GetInteger("id").ShouldBe(8863);
    }

    [Fact]
    public void User_route_resolves_id()
    {
        var route = CreateRouter().Resolve("/user/pg");

        route.Name.ShouldBe("user");
        route.Parameters.GetString("id").ShouldBe("pg");
    }

    [Fact]
    public void Static_item_segment_outranks_the_dynamic_feed_route()
    {
        // /item/42 must match the item route, not /:feed/:page (specificity: static > dynamic).
        CreateRouter().Resolve("/item/42").Name.ShouldBe("item");
    }

    [Fact]
    public void Non_numeric_item_id_falls_through_to_not_found()
    {
        // /item/:id(\d+) rejects a non-numeric id, and the second segment blocks the feed route too.
        CreateRouter().Resolve("/item/abc").Name.ShouldBe("not-found");
    }

    [Fact]
    public void Deep_unknown_path_resolves_to_not_found()
    {
        CreateRouter().Resolve("/a/b/c/d").Name.ShouldBe("not-found");
    }

    [Fact]
    public async Task Root_redirects_to_the_top_feed_in_session()
    {
        // The BeforeEach root redirect fires for an in-session navigation to "/" (e.g. clicking the
        // logo). Navigate away from the router's pre-resolved initial "/" first, so this Push is not a
        // deduplicated same-location no-op. (The initial page-load "/" case is handled in bootstrap —
        // Program.Main — because the router resolves its initial location without running guards; see
        // [V01.01.08.07], #219.)
        var router = CreateRouter();
        await router.Push("/new");

        await router.Push("/");

        router.CurrentRoute.Value.Name.ShouldBe("feed");
        router.CurrentRoute.Value.Parameters.GetString("feed").ShouldBe("top");
    }

    [Fact]
    public async Task Navigating_a_feed_page_updates_the_current_route()
    {
        var router = CreateRouter();

        await router.Push("/show/2");

        router.CurrentRoute.Value.Name.ShouldBe("feed");
        router.CurrentRoute.Value.Parameters.GetString("feed").ShouldBe("show");
        router.CurrentRoute.Value.Parameters.GetInteger("page").ShouldBe(2);
    }
}
