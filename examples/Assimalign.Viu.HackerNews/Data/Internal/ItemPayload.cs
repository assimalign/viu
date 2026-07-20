namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The wire shape of a HackerNews <c>/v0/item/{id}</c> response, deserialized by the
/// source-generated <see cref="HackerNewsJsonSerializerContext"/> (never by reflection).
/// Mutable auto-properties are the DTO surface System.Text.Json's source generator binds to;
/// <see cref="HackerNewsClient"/> maps this to the immutable <see cref="HackerNewsItem"/> domain
/// record. Property names bind case-insensitively to the API's lowercase JSON keys.
/// </summary>
internal sealed class ItemPayload
{
    public long Id { get; set; }

    public string? Type { get; set; }

    public string? By { get; set; }

    /// <summary>Submission time in Unix seconds (mapped to a <see cref="System.DateTimeOffset"/>).</summary>
    public long Time { get; set; }

    public string? Title { get; set; }

    public string? Url { get; set; }

    public string? Text { get; set; }

    public int Score { get; set; }

    public int Descendants { get; set; }

    public long[]? Kids { get; set; }

    public long Parent { get; set; }

    public bool Deleted { get; set; }

    public bool Dead { get; set; }
}
