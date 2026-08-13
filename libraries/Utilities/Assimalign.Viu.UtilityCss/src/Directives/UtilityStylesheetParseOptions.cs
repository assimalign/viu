namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Immutable host context for one I/O-free project stylesheet parse.
/// </summary>
public sealed record UtilityStylesheetParseOptions
{
    /// <summary>
    /// Gets the default source context.
    /// </summary>
    public static UtilityStylesheetParseOptions Default { get; } = new();

    /// <summary>
    /// Gets the optional source identity copied onto all parsed nodes and diagnostics.
    /// </summary>
    public string? SourceIdentity { get; init; }

    /// <summary>
    /// Gets the non-negative absolute offset added to every source-relative span.
    /// </summary>
    public int ContentOffset { get; init; }
}
