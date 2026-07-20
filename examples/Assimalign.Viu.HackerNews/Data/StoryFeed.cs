namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The five HackerNews story feeds this sample surfaces, mirroring the front-page tabs of
/// vuejs/vue-hackernews-2.0 (top / new / show / ask / jobs). Each maps to a HackerNews API
/// story-id list endpoint (see <see cref="StoryFeeds"/>).
/// </summary>
internal enum StoryFeed
{
    /// <summary>The front-page "top" ranking (<c>topstories.json</c>).</summary>
    Top,

    /// <summary>The newest submissions (<c>newstories.json</c>).</summary>
    New,

    /// <summary>Show HN submissions (<c>showstories.json</c>).</summary>
    Show,

    /// <summary>Ask HN submissions (<c>askstories.json</c>).</summary>
    Ask,

    /// <summary>Job postings (<c>jobstories.json</c>).</summary>
    Jobs,
}
