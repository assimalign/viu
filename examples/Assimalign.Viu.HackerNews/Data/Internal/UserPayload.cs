namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The wire shape of a HackerNews <c>/v0/user/{id}</c> response, deserialized by the
/// source-generated <see cref="HackerNewsJsonSerializerContext"/>. Mapped to the immutable
/// <see cref="HackerNewsUser"/> domain record by <see cref="HackerNewsClient"/>.
/// </summary>
internal sealed class UserPayload
{
    public string? Id { get; set; }

    /// <summary>Account creation time in Unix seconds (mapped to a <see cref="System.DateTimeOffset"/>).</summary>
    public long Created { get; set; }

    public int Karma { get; set; }

    public string? About { get; set; }

    public long[]? Submitted { get; set; }
}
