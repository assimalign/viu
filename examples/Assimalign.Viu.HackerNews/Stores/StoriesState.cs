using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The reactive state of <see cref="StoriesStore"/> — the current feed page. The
/// <c>[Reactive]</c> source generator (Vue's <c>reactive()</c>, no proxy) emits per-member
/// dependencies, so a view reading <see cref="Items"/> re-renders only when the list is replaced,
/// and a view reading <see cref="IsLoading"/> re-renders only when loading flips. Loading and error
/// are modeled as explicit state (#103), never thrown out of the store.
/// </summary>
[Reactive]
internal partial class StoriesState
{
    /// <summary>The feed the current page belongs to.</summary>
    public partial StoryFeed ActiveFeed { get; set; }

    /// <summary>The 1-based page number.</summary>
    public partial int ActivePage { get; set; }

    /// <summary>The total story count in the active feed (drives pagination bounds).</summary>
    public partial int TotalCount { get; set; }

    /// <summary>Whether a load is in flight.</summary>
    public partial bool IsLoading { get; set; }

    /// <summary>The last load error message, or null.</summary>
    public partial string? Error { get; set; }

    /// <summary>The stories on the current page, in rank order, each with a stable <see cref="HackerNewsItem.Id"/>.</summary>
    public partial IReadOnlyList<HackerNewsItem> Items { get; set; }
}
