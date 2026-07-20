using System;

using Assimalign.Viu.HackerNews;

namespace Assimalign.Viu.HackerNews.Tests;

/// <summary>Terse builders for the domain records used across the tests.</summary>
internal static class TestData
{
    private static readonly DateTimeOffset When = DateTimeOffset.FromUnixTimeSeconds(1_200_000_000);

    public static HackerNewsItem Story(
        long id,
        string title,
        int score = 10,
        string by = "alice",
        int descendants = 0,
        string? url = null,
        long[]? kids = null)
        => new(
            id,
            "story",
            by,
            When,
            title,
            url,
            HackerNewsClient.ToHost(url),
            null,
            score,
            descendants,
            kids ?? Array.Empty<long>(),
            0,
            Deleted: false,
            Dead: false);

    public static HackerNewsItem Comment(
        long id,
        string text,
        string by = "bob",
        long[]? kids = null,
        bool deleted = false,
        bool dead = false)
        => new(
            id,
            "comment",
            by,
            When,
            null,
            null,
            null,
            text,
            0,
            0,
            kids ?? Array.Empty<long>(),
            0,
            deleted,
            dead);

    public static HackerNewsUser User(string id, int karma = 100, string? about = null, int submitted = 0)
        => new(id, When, karma, about, submitted);
}
