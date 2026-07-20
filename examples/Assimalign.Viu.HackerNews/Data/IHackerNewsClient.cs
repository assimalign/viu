using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The sample's data-layer seam: every network read goes through this typed abstraction, never
/// through ad-hoc component fetches. The browser app binds it to <see cref="HackerNewsClient"/>
/// (HttpClient + source-generated <see cref="System.Text.Json"/>); tests and any future prerenderer
/// bind a fake, satisfying the "isolated behind an injectable API-client abstraction so the data
/// layer can be swapped for prerendering and tests" acceptance criterion (#103).
/// <para>
/// All methods are async and non-blocking, honoring the single-threaded WASM event loop, and take a
/// <see cref="CancellationToken"/> so a store can cancel a superseded in-flight load.
/// </para>
/// </summary>
internal interface IHackerNewsClient
{
    /// <summary>Fetches the ordered story-id list for <paramref name="feed"/>.</summary>
    /// <param name="feed">The feed whose ranking to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The story ids in display order (empty when the feed is empty).</returns>
    Task<IReadOnlyList<long>> GetFeedAsync(StoryFeed feed, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single item (story, comment, job, or poll) by id.</summary>
    /// <param name="id">The item id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or null when it does not exist.</returns>
    Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Fetches a user profile by id.</summary>
    /// <param name="id">The case-sensitive user id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The profile, or null when it does not exist.</returns>
    Task<HackerNewsUser?> GetUserAsync(string id, CancellationToken cancellationToken = default);
}
