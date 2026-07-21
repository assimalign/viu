using System;
using System.Threading.Tasks;

using Assimalign.Viu.HackerNews;
using Assimalign.Viu.Router;
using Assimalign.Viu.Store;
using Assimalign.Viu.Testing;

using Shouldly;

using Xunit;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Viu.HackerNews.Tests;

/// <summary>
/// Covers the views end-to-end through the in-memory Testing renderer: the story list renders from
/// store state, and the loading and error states render their own UI. The router (memory history) and
/// the store-definitions container are provided into the tree; the store registry is the active one so
/// the views' argument-less <c>UseStore()</c> resolves it.
/// </summary>
public sealed class ViewTests
{
    private static ViuRouter RouterAt(string path)
    {
        var router = new ViuRouter(RouterHistory.CreateMemory(), AppRoutes.Create());
        router.BeforeEach(AppRoutes.RedirectRoot);
        _ = router.Push(path);
        return router;
    }

    private static ComponentMountOptions Options(ViuRouter router, HackerNewsStores stores)
        => new ComponentMountOptions
        {
            // The store-definitions container resolves through the app service provider (the shape the
            // browser bootstrap uses); the router stays provided under its injection key.
            Services = new ServiceProviderBuilder().AddSingleton(stores).Build(),
        }
        .Provide(RouterInjectionKeys.Router, router);

    private static async Task WithActiveRegistry(HackerNewsStores stores, StoreRegistry registry, Func<Task> body)
    {
        Stores.SetActiveRegistry(registry);
        try
        {
            await body();
        }
        finally
        {
            Stores.SetActiveRegistry(null);
            registry.Dispose();
        }
    }

    [Fact]
    public async Task Story_list_renders_keyed_rows_from_the_store()
    {
        var client = new FakeHackerNewsClient().AddFeed(StoryFeed.Top, 1, 2)
            .AddItem(TestData.Story(1, "Alpha", score: 42))
            .AddItem(TestData.Story(2, "Beta", url: "https://example.com/beta"));
        var stores = new HackerNewsStores(client);
        var router = RouterAt("/top");
        var registry = new StoreRegistry();

        await WithActiveRegistry(stores, registry, async () =>
        {
            using var wrapper = ViuTest.Mount(StoriesView.Instance, Options(router, stores));
            await wrapper.FlushAsync();
            await wrapper.NextTickAsync();

            var html = wrapper.Html();
            html.ShouldContain("Alpha");
            html.ShouldContain("Beta");
            html.ShouldContain("42 points");
            html.ShouldContain("example.com");   // host of the external story
            html.ShouldContain("/item/1");        // internal (self) story links to its discussion
        });
    }

    [Fact]
    public async Task Story_list_shows_the_loading_state_while_the_feed_is_pending()
    {
        var client = new FakeHackerNewsClient().AddFeed(StoryFeed.Top, 1).AddItem(TestData.Story(1, "Alpha"));
        client.FeedGate = new System.Threading.Tasks.TaskCompletionSource();
        var stores = new HackerNewsStores(client);
        var router = RouterAt("/top");
        var registry = new StoreRegistry();

        await WithActiveRegistry(stores, registry, async () =>
        {
            using var wrapper = ViuTest.Mount(StoriesView.Instance, Options(router, stores));
            await wrapper.FlushAsync();

            wrapper.Html().ShouldContain("Loading");

            client.FeedGate!.SetResult();
            await wrapper.FlushAsync();
            await wrapper.NextTickAsync();

            wrapper.Html().ShouldContain("Alpha");
        });
    }

    [Fact]
    public async Task Story_list_shows_the_error_state_on_failure()
    {
        var client = new FakeHackerNewsClient { FeedError = new System.InvalidOperationException("offline") };
        var stores = new HackerNewsStores(client);
        var router = RouterAt("/top");
        var registry = new StoreRegistry();

        await WithActiveRegistry(stores, registry, async () =>
        {
            using var wrapper = ViuTest.Mount(StoriesView.Instance, Options(router, stores));
            await wrapper.FlushAsync();
            await wrapper.NextTickAsync();

            var html = wrapper.Html();
            html.ShouldContain("offline");
        });
    }

    [Fact]
    public async Task Unknown_feed_renders_a_not_found_message()
    {
        var stores = new HackerNewsStores(new FakeHackerNewsClient());
        var router = RouterAt("/nonsense");
        var registry = new StoreRegistry();

        await WithActiveRegistry(stores, registry, async () =>
        {
            using var wrapper = ViuTest.Mount(StoriesView.Instance, Options(router, stores));
            await wrapper.FlushAsync();
            await wrapper.NextTickAsync();

            wrapper.Html().ShouldContain("nonsense");
        });
    }
}
