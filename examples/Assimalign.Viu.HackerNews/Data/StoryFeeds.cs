using System;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// Reflection-free (AOT/trimming-safe) mapping between <see cref="StoryFeed"/> values and their
/// URL slug, HackerNews API endpoint, and display label. Every mapping is an explicit switch — the
/// sample never uses <c>Enum.Parse</c>/reflection so nothing here fights the trimmer.
/// </summary>
internal static class StoryFeeds
{
    /// <summary>All feeds in navigation order (the header tabs render from this).</summary>
    public static readonly StoryFeed[] All =
    [
        StoryFeed.Top,
        StoryFeed.New,
        StoryFeed.Show,
        StoryFeed.Ask,
        StoryFeed.Jobs,
    ];

    /// <summary>The URL slug for <paramref name="feed"/> (the <c>:feed</c> route parameter value).</summary>
    public static string ToSlug(StoryFeed feed) => feed switch
    {
        StoryFeed.Top => "top",
        StoryFeed.New => "new",
        StoryFeed.Show => "show",
        StoryFeed.Ask => "ask",
        StoryFeed.Jobs => "jobs",
        _ => "top",
    };

    /// <summary>The human label for <paramref name="feed"/> (header tab text).</summary>
    public static string ToLabel(StoryFeed feed) => feed switch
    {
        StoryFeed.Top => "Top",
        StoryFeed.New => "New",
        StoryFeed.Show => "Show",
        StoryFeed.Ask => "Ask",
        StoryFeed.Jobs => "Jobs",
        _ => "Top",
    };

    /// <summary>
    /// The HackerNews API relative endpoint for <paramref name="feed"/>'s story-id list, e.g.
    /// <c>v0/topstories.json</c> (https://github.com/HackerNews/API#new-top-and-best-stories).
    /// </summary>
    public static string ToEndpoint(StoryFeed feed) => feed switch
    {
        StoryFeed.Top => "v0/topstories.json",
        StoryFeed.New => "v0/newstories.json",
        StoryFeed.Show => "v0/showstories.json",
        StoryFeed.Ask => "v0/askstories.json",
        StoryFeed.Jobs => "v0/jobstories.json",
        _ => "v0/topstories.json",
    };

    /// <summary>
    /// Parses a route slug back to a <see cref="StoryFeed"/>. Returns false for an unknown slug so a
    /// view can fall back to a not-found rendering (the <c>:feed</c> route parameter is unconstrained).
    /// </summary>
    /// <param name="slug">The <c>:feed</c> parameter value.</param>
    /// <param name="feed">The parsed feed when recognized.</param>
    /// <returns>Whether <paramref name="slug"/> names a known feed.</returns>
    public static bool TryParse(string? slug, out StoryFeed feed)
    {
        switch (slug)
        {
            case "top":
                feed = StoryFeed.Top;
                return true;
            case "new":
                feed = StoryFeed.New;
                return true;
            case "show":
                feed = StoryFeed.Show;
                return true;
            case "ask":
                feed = StoryFeed.Ask;
                return true;
            case "jobs":
                feed = StoryFeed.Jobs;
                return true;
            default:
                feed = StoryFeed.Top;
                return false;
        }
    }
}
