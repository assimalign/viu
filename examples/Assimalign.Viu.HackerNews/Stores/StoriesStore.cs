using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The Pinia-style store for the paged story lists (top/new/show/ask/jobs) — the C# port of
/// vue-hackernews-2.0's list module. All fetched list state flows through here, never through
/// per-component fields (#103): a view calls <see cref="LoadPageAsync"/> and renders from
/// <see cref="Store{TState}.State"/>. Built on the <c>Store&lt;TState&gt;</c> member model over
/// <see cref="StoriesState"/>, with a computed getter (<see cref="PageCount"/>) and the load action.
/// </summary>
internal sealed class StoriesStore : Store<StoriesState>
{
    /// <summary>Stories per page (vue-hackernews-2.0 parity).</summary>
    public const int PageSize = 20;

    private readonly IHackerNewsClient _client;

    // The feed id-lists are large and stable within a session; cache them so paging within a feed
    // fetches only the page's items, not the ranking again. Non-reactive: it is a fetch cache, not view state.
    private readonly Dictionary<StoryFeed, IReadOnlyList<long>> _feedCache = new();

    // Cancels a superseded load so a slow earlier page cannot clobber a newer one's state.
    private CancellationTokenSource? _inFlight;

    /// <summary>Creates the store over <paramref name="client"/>.</summary>
    /// <param name="client">The data client all reads go through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public StoriesStore(IHackerNewsClient client)
        : base(
            "stories",
            static () => new StoriesState
            {
                ActiveFeed = StoryFeed.Top,
                ActivePage = 1,
                TotalCount = 0,
                IsLoading = false,
                Error = null,
                Items = Array.Empty<HackerNewsItem>(),
            },
            static (target, source) =>
            {
                target.ActiveFeed = source.ActiveFeed;
                target.ActivePage = source.ActivePage;
                target.TotalCount = source.TotalCount;
                target.IsLoading = source.IsLoading;
                target.Error = source.Error;
                target.Items = source.Items;
            })
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        PageCount = Reactive.Computed(() =>
        {
            var total = State.TotalCount;
            return total <= 0 ? 0 : (total + PageSize - 1) / PageSize;
        });
    }

    /// <summary>The number of pages in the active feed (a computed getter over <see cref="StoriesState.TotalCount"/>).</summary>
    public Computed<int> PageCount { get; }

    /// <summary>
    /// Loads page <paramref name="page"/> of <paramref name="feed"/> into the store, modeling loading
    /// and error as state. A newer call cancels an in-flight older one, so navigation never leaves a
    /// stale page rendered. Never throws — a failure lands in <see cref="StoriesState.Error"/>.
    /// </summary>
    /// <param name="feed">The feed to load.</param>
    /// <param name="page">The 1-based page number (clamped to at least 1).</param>
    public async Task LoadPageAsync(StoryFeed feed, int page)
    {
        if (page < 1)
        {
            page = 1;
        }

        _inFlight?.Cancel();
        _inFlight?.Dispose();
        var cancellation = new CancellationTokenSource();
        _inFlight = cancellation;
        var token = cancellation.Token;

        Patch(state =>
        {
            state.ActiveFeed = feed;
            state.ActivePage = page;
            state.IsLoading = true;
            state.Error = null;
        });

        try
        {
            if (!_feedCache.TryGetValue(feed, out var ids))
            {
                ids = await _client.GetFeedAsync(feed, token);
                token.ThrowIfCancellationRequested();
                _feedCache[feed] = ids;
            }

            var start = (page - 1) * PageSize;
            var pageIds = new List<long>(PageSize);
            for (var index = start; index < ids.Count && index < start + PageSize; index++)
            {
                pageIds.Add(ids[index]);
            }

            var items = await FetchItemsAsync(pageIds, token);
            token.ThrowIfCancellationRequested();

            Patch(state =>
            {
                state.TotalCount = ids.Count;
                state.Items = items;
                state.IsLoading = false;
                state.Error = null;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load; the newer load owns the state.
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                Patch(state =>
                {
                    state.IsLoading = false;
                    state.Error = exception.Message;
                });
            }
        }
    }

    // Fetches the page's items concurrently (fetch is async I/O; this respects the single-threaded WASM
    // loop — no blocking waits) and preserves rank order, dropping deleted/dead items and holes.
    private async Task<IReadOnlyList<HackerNewsItem>> FetchItemsAsync(IReadOnlyList<long> ids, CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<HackerNewsItem>();
        }
        var pending = new Task<HackerNewsItem?>[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            pending[index] = _client.GetItemAsync(ids[index], token);
        }
        var fetched = await Task.WhenAll(pending);
        var items = new List<HackerNewsItem>(fetched.Length);
        foreach (var item in fetched)
        {
            if (item is not null && !item.Deleted && !item.Dead)
            {
                items.Add(item);
            }
        }
        return items;
    }
}
