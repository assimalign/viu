using System;
using System.Threading.Tasks;

using Assimalign.Viu.HackerNews;
using Assimalign.Viu.Store;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.HackerNews.Tests;

/// <summary>
/// Covers the stores' observable behavior with a fake client: loading/error modeling, filtering,
/// pagination, the feed cache, comment-tree assembly, and superseded-load cancellation. State is
/// asserted directly (the reactive setters run synchronously inside the store's <c>Patch</c>), so no
/// scheduler pump is needed. All fetched state flows through the store (#103).
/// </summary>
public sealed class StoreTests
{
    private static StoriesStore Stories(FakeHackerNewsClient client)
        => new HackerNewsStores(client).Stories.UseStore(new StoreRegistry());

    private static ItemStore Item(FakeHackerNewsClient client)
        => new HackerNewsStores(client).Item.UseStore(new StoreRegistry());

    private static UserStore Users(FakeHackerNewsClient client)
        => new HackerNewsStores(client).User.UseStore(new StoreRegistry());

    // ---- StoriesStore ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadPage_populates_items_in_order_and_clears_loading()
    {
        var client = new FakeHackerNewsClient().AddFeed(StoryFeed.Top, 1, 2, 3)
            .AddItem(TestData.Story(1, "First"))
            .AddItem(TestData.Story(2, "Second"))
            .AddItem(TestData.Story(3, "Third"));
        var store = Stories(client);

        await store.LoadPageAsync(StoryFeed.Top, 1);

        store.State.IsLoading.ShouldBeFalse();
        store.State.Error.ShouldBeNull();
        store.State.TotalCount.ShouldBe(3);
        store.State.Items.Count.ShouldBe(3);
        store.State.Items[0].Id.ShouldBe(1L);
        store.State.Items[2].Id.ShouldBe(3L);
    }

    [Fact]
    public async Task LoadPage_drops_missing_and_dead_items()
    {
        var client = new FakeHackerNewsClient().AddFeed(StoryFeed.Top, 1, 2, 3, 4)
            .AddItem(TestData.Story(1, "Kept"))
            .AddItem(TestData.Story(3, "Dead") with { Dead = true })
            .AddItem(TestData.Story(4, "Also kept"));
        // id 2 has no item (null); id 3 is dead — both dropped, order preserved.
        var store = Stories(client);

        await store.LoadPageAsync(StoryFeed.Top, 1);

        store.State.Items.Count.ShouldBe(2);
        store.State.Items[0].Id.ShouldBe(1L);
        store.State.Items[1].Id.ShouldBe(4L);
        store.State.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task LoadPage_paginates_and_computes_page_count()
    {
        var ids = new long[25];
        var client = new FakeHackerNewsClient();
        for (var i = 0; i < 25; i++)
        {
            ids[i] = i + 1;
            client.AddItem(TestData.Story(i + 1, $"Story {i + 1}"));
        }
        client.AddFeed(StoryFeed.New, ids);
        var store = Stories(client);

        await store.LoadPageAsync(StoryFeed.New, 2);

        store.State.ActivePage.ShouldBe(2);
        store.State.Items.Count.ShouldBe(5); // items 21..25
        store.State.Items[0].Id.ShouldBe(21L);
        store.PageCount.Value.ShouldBe(2); // ceil(25 / 20)
    }

    [Fact]
    public async Task LoadPage_models_a_fetch_error_as_state()
    {
        var client = new FakeHackerNewsClient { FeedError = new InvalidOperationException("boom") };
        var store = Stories(client);

        await store.LoadPageAsync(StoryFeed.Top, 1);

        store.State.IsLoading.ShouldBeFalse();
        store.State.Error.ShouldBe("boom");
        store.State.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadPage_caches_the_feed_id_list_across_pages()
    {
        var ids = new long[25];
        var client = new FakeHackerNewsClient();
        for (var i = 0; i < 25; i++)
        {
            ids[i] = i + 1;
            client.AddItem(TestData.Story(i + 1, $"Story {i + 1}"));
        }
        client.AddFeed(StoryFeed.Top, ids);
        var store = Stories(client);

        await store.LoadPageAsync(StoryFeed.Top, 1);
        await store.LoadPageAsync(StoryFeed.Top, 2);

        client.FeedCalls.ShouldBe(1); // the id list is fetched once, then paged from cache
    }

    [Fact]
    public async Task A_superseded_load_does_not_clobber_the_newer_one()
    {
        var client = new FakeHackerNewsClient()
            .AddFeed(StoryFeed.Top, 1).AddItem(TestData.Story(1, "Old"))
            .AddFeed(StoryFeed.New, 10).AddItem(TestData.Story(10, "New"));
        client.FeedGate = new TaskCompletionSource();
        var store = Stories(client);

        var first = store.LoadPageAsync(StoryFeed.Top, 1);  // suspends at the gate
        var second = store.LoadPageAsync(StoryFeed.New, 1); // cancels the first, also suspends
        client.FeedGate.SetResult();
        await Task.WhenAll(first, second);

        store.State.ActiveFeed.ShouldBe(StoryFeed.New);
        store.State.Items.Count.ShouldBe(1);
        store.State.Items[0].Id.ShouldBe(10L);
        store.State.Error.ShouldBeNull();
    }

    // ---- ItemStore ------------------------------------------------------------------------------

    [Fact]
    public async Task LoadItem_builds_a_bounded_comment_tree()
    {
        var client = new FakeHackerNewsClient()
            .AddItem(TestData.Story(100, "Story", descendants: 3, kids: [101, 102]))
            .AddItem(TestData.Comment(101, "top comment", kids: [201]))
            .AddItem(TestData.Comment(102, "another top"))
            .AddItem(TestData.Comment(201, "reply", kids: [301])); // 201 sits at MaxDepth, its kids aren't fetched
        var store = Item(client);

        await store.LoadItemAsync(100);

        store.State.Story!.Id.ShouldBe(100L);
        store.State.Comments.Count.ShouldBe(2);
        store.State.Comments[0].Comment.Id.ShouldBe(101L);
        store.State.Comments[0].Replies.Count.ShouldBe(1);
        store.State.Comments[0].Replies[0].Comment.Id.ShouldBe(201L);
        store.State.Comments[0].Replies[0].HasMoreReplies.ShouldBeTrue(); // depth bound reached
        store.State.Comments[1].Comment.Id.ShouldBe(102L);
        store.State.Comments[1].Replies.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadItem_models_a_missing_item_as_an_error()
    {
        var store = Item(new FakeHackerNewsClient());

        await store.LoadItemAsync(999);

        store.State.Story.ShouldBeNull();
        store.State.IsLoading.ShouldBeFalse();
        store.State.Error.ShouldNotBeNull();
    }

    // ---- UserStore ------------------------------------------------------------------------------

    [Fact]
    public async Task LoadUser_populates_the_profile()
    {
        var client = new FakeHackerNewsClient().AddUser(TestData.User("pg", karma: 155000));
        var store = Users(client);

        await store.LoadUserAsync("pg");

        store.State.Profile!.Id.ShouldBe("pg");
        store.State.Profile!.Karma.ShouldBe(155000);
        store.State.Error.ShouldBeNull();
    }

    [Fact]
    public async Task LoadUser_models_a_missing_user_as_an_error()
    {
        var store = Users(new FakeHackerNewsClient());

        await store.LoadUserAsync("ghost");

        store.State.Profile.ShouldBeNull();
        store.State.Error.ShouldNotBeNull();
    }
}
