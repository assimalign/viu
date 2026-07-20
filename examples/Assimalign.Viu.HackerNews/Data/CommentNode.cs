using System.Collections.Generic;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// A node in a fetched comment tree: the <see cref="Comment"/> item plus its already-loaded
/// <see cref="Replies"/>. The item-detail store assembles this tree (depth- and count-bounded so a
/// large thread never triggers a fetch storm), and the recursive comment view renders it with each
/// node keyed by its comment id.
/// </summary>
/// <param name="Comment">The comment item.</param>
/// <param name="Replies">The loaded child replies (may be empty even when the comment has more kids beyond the fetch bound).</param>
/// <param name="HasMoreReplies">Whether the comment has child kids that were not fetched (the bound was reached).</param>
internal sealed record CommentNode(
    HackerNewsItem Comment,
    IReadOnlyList<CommentNode> Replies,
    bool HasMoreReplies);
