using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The reactive state of <see cref="ItemStore"/> — one story and its bounded comment tree, plus
/// explicit loading/error. Per-member reactivity means the header and the comment tree re-render
/// independently as each lands.
/// </summary>
[Reactive]
internal partial class ItemState
{
    /// <summary>The item id currently being shown.</summary>
    public partial long ActiveId { get; set; }

    /// <summary>The loaded story/item, or null while loading or when missing.</summary>
    public partial HackerNewsItem? Story { get; set; }

    /// <summary>The loaded comment tree (bounded depth/count), each node keyed by its comment id.</summary>
    public partial IReadOnlyList<CommentNode> Comments { get; set; }

    /// <summary>Whether a load is in flight.</summary>
    public partial bool IsLoading { get; set; }

    /// <summary>The last load error message, or null (includes the "not found" case).</summary>
    public partial string? Error { get; set; }
}
