using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The live <see cref="IHackerNewsClient"/>: reads the public HackerNews Firebase API
/// (<c>https://hacker-news.firebaseio.com/v0/</c>, https://github.com/HackerNews/API) over
/// <see cref="HttpClient"/> and deserializes with the source-generated
/// <see cref="HackerNewsJsonSerializerContext"/> — no reflection-based serialization anywhere, so the
/// trimmed WASM publish stays clean. In the browser the injected <see cref="HttpClient"/> is backed
/// by the fetch handler; the API sends permissive CORS headers, so the client can read it directly.
/// </summary>
internal sealed class HackerNewsClient : IHackerNewsClient
{
    /// <summary>The HackerNews Firebase API base address (v0).</summary>
    public static readonly Uri BaseAddress = new("https://hacker-news.firebaseio.com/");

    private readonly HttpClient _httpClient;

    /// <summary>Creates the client over <paramref name="httpClient"/> (its base address must be <see cref="BaseAddress"/>).</summary>
    /// <param name="httpClient">The HTTP client to read the API with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is null.</exception>
    public HackerNewsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetFeedAsync(StoryFeed feed, CancellationToken cancellationToken = default)
    {
        // The story-id list is a bare JSON array of numbers; JsonDocument reads it reflection-free and
        // trim-safe, so no source-generated array metadata is needed (see HackerNewsJsonSerializerContext).
        var json = await _httpClient.GetStringAsync(StoryFeeds.ToEndpoint(feed), cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<long>();
        }
        var ids = new List<long>(root.GetArrayLength());
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                ids.Add(element.GetInt64());
            }
        }
        return ids;
    }

    /// <inheritdoc />
    public async Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default)
    {
        var json = await _httpClient.GetStringAsync($"v0/item/{id}.json", cancellationToken);
        // The API returns the JSON literal null for a missing item.
        var payload = JsonSerializer.Deserialize(json, HackerNewsJsonSerializerContext.Default.ItemPayload);
        return payload is null ? null : MapItem(payload);
    }

    /// <inheritdoc />
    public async Task<HackerNewsUser?> GetUserAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var json = await _httpClient.GetStringAsync($"v0/user/{Uri.EscapeDataString(id)}.json", cancellationToken);
        var payload = JsonSerializer.Deserialize(json, HackerNewsJsonSerializerContext.Default.UserPayload);
        return payload is null || payload.Id is null ? null : MapUser(payload);
    }

    private static HackerNewsItem MapItem(ItemPayload payload) => new(
        payload.Id,
        payload.Type ?? "story",
        payload.By,
        DateTimeOffset.FromUnixTimeSeconds(payload.Time),
        payload.Title,
        payload.Url,
        ToHost(payload.Url),
        payload.Text,
        payload.Score,
        payload.Descendants,
        payload.Kids ?? Array.Empty<long>(),
        payload.Parent,
        payload.Deleted,
        payload.Dead);

    private static HackerNewsUser MapUser(UserPayload payload) => new(
        payload.Id!,
        DateTimeOffset.FromUnixTimeSeconds(payload.Created),
        payload.Karma,
        payload.About,
        payload.Submitted?.Length ?? 0);

    /// <summary>Derives the display host (without a leading <c>www.</c>) from a story URL.</summary>
    /// <param name="url">The story URL, or null.</param>
    /// <returns>The host, or null when there is no absolute URL.</returns>
    internal static string? ToHost(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
