using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Store;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The Pinia-style store for the item-detail page — a story and its comment thread (the C# port of
/// vue-hackernews-2.0's item view). It assembles a comment <b>tree</b> from the flat HackerNews item
/// graph, bounded by <see cref="MaxDepth"/> and <see cref="MaxTotalComments"/> so a large thread
/// never triggers a client-side fetch storm. Loading and error are explicit state; a superseded load
/// is cancelled.
/// </summary>
internal sealed class ItemStore : Store<ItemState>
{
    /// <summary>How many comment levels to fetch (top level + replies).</summary>
    public const int MaxDepth = 2;

    /// <summary>The ceiling on total comment fetches per thread (bounds network on huge threads).</summary>
    public const int MaxTotalComments = 60;

    private readonly IHackerNewsClient _client;
    private CancellationTokenSource? _inFlight;

    /// <summary>Creates the store over <paramref name="client"/>.</summary>
    /// <param name="client">The data client all reads go through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public ItemStore(IHackerNewsClient client)
        : base(
            "item",
            static () => new ItemState
            {
                ActiveId = 0,
                Story = null,
                Comments = Array.Empty<CommentNode>(),
                IsLoading = false,
                Error = null,
            },
            static (target, source) =>
            {
                target.ActiveId = source.ActiveId;
                target.Story = source.Story;
                target.Comments = source.Comments;
                target.IsLoading = source.IsLoading;
                target.Error = source.Error;
            })
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Loads the story <paramref name="id"/> and its bounded comment tree, modeling loading and error
    /// as state. A newer call cancels an in-flight older one. Never throws.
    /// </summary>
    /// <param name="id">The story/item id.</param>
    public async Task LoadItemAsync(long id)
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        var cancellation = new CancellationTokenSource();
        _inFlight = cancellation;
        var token = cancellation.Token;

        Patch(state =>
        {
            state.ActiveId = id;
            state.Story = null;
            state.Comments = Array.Empty<CommentNode>();
            state.IsLoading = true;
            state.Error = null;
        });

        try
        {
            var story = await _client.GetItemAsync(id, token);
            token.ThrowIfCancellationRequested();
            if (story is null)
            {
                Patch(state =>
                {
                    state.IsLoading = false;
                    state.Error = "That item does not exist.";
                });
                return;
            }

            var budget = new int[] { MaxTotalComments };
            var comments = await BuildTreeAsync(story.Kids, depth: 1, budget, token);
            token.ThrowIfCancellationRequested();

            Patch(state =>
            {
                state.Story = story;
                state.Comments = comments;
                state.IsLoading = false;
                state.Error = null;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load.
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

    // Depth-first, level-parallel, budget-bounded assembly of the comment tree from the flat item graph.
    private async Task<IReadOnlyList<CommentNode>> BuildTreeAsync(
        IReadOnlyList<long> kids,
        int depth,
        int[] budget,
        CancellationToken token)
    {
        if (kids.Count == 0 || depth > MaxDepth || budget[0] <= 0)
        {
            return Array.Empty<CommentNode>();
        }

        var take = Math.Min(kids.Count, budget[0]);
        var pending = new Task<HackerNewsItem?>[take];
        for (var index = 0; index < take; index++)
        {
            pending[index] = _client.GetItemAsync(kids[index], token);
        }
        var fetched = await Task.WhenAll(pending);
        budget[0] -= take;

        var nodes = new List<CommentNode>(take);
        foreach (var comment in fetched)
        {
            if (comment is null || comment.Deleted || comment.Dead)
            {
                continue;
            }
            var replies = depth < MaxDepth
                ? await BuildTreeAsync(comment.Kids, depth + 1, budget, token)
                : Array.Empty<CommentNode>();
            var hasMoreReplies = comment.Kids.Count > replies.Count;
            nodes.Add(new CommentNode(comment, replies, hasMoreReplies));
        }
        return nodes;
    }
}
