using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.HackerNews;

namespace Assimalign.Viu.HackerNews.Tests;

/// <summary>
/// An in-memory <see cref="IHackerNewsClient"/> for tests — the swap the injectable data seam exists
/// for (#103). Serves canned feeds/items/users, counts calls, honors cancellation, and can inject an
/// error or gate the feed read (for the store's superseded-load cancellation test). No network.
/// </summary>
internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private readonly Dictionary<StoryFeed, IReadOnlyList<long>> _feeds = new();
    private readonly Dictionary<long, HackerNewsItem> _items = new();
    private readonly Dictionary<string, HackerNewsUser> _users = new();

    /// <summary>Number of <see cref="GetFeedAsync"/> calls.</summary>
    public int FeedCalls { get; private set; }

    /// <summary>Number of <see cref="GetItemAsync"/> calls.</summary>
    public int ItemCalls { get; private set; }

    /// <summary>Number of <see cref="GetUserAsync"/> calls.</summary>
    public int UserCalls { get; private set; }

    /// <summary>When set, <see cref="GetFeedAsync"/> awaits this before returning (for ordering/cancellation tests).</summary>
    public TaskCompletionSource? FeedGate { get; set; }

    /// <summary>When set, <see cref="GetFeedAsync"/> throws it (error-path tests).</summary>
    public Exception? FeedError { get; set; }

    public FakeHackerNewsClient AddFeed(StoryFeed feed, params long[] ids)
    {
        _feeds[feed] = ids;
        return this;
    }

    public FakeHackerNewsClient AddItem(HackerNewsItem item)
    {
        _items[item.Id] = item;
        return this;
    }

    public FakeHackerNewsClient AddUser(HackerNewsUser user)
    {
        _users[user.Id] = user;
        return this;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetFeedAsync(StoryFeed feed, CancellationToken cancellationToken = default)
    {
        FeedCalls++;
        if (FeedGate is not null)
        {
            await FeedGate.Task;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (FeedError is not null)
        {
            throw FeedError;
        }
        return _feeds.TryGetValue(feed, out var ids) ? ids : Array.Empty<long>();
    }

    /// <inheritdoc />
    public Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default)
    {
        ItemCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);
    }

    /// <inheritdoc />
    public Task<HackerNewsUser?> GetUserAsync(string id, CancellationToken cancellationToken = default)
    {
        UserCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);
    }
}
