namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Immutable host context for one I/O-free utility candidate scan.
/// </summary>
public sealed record UtilityCandidateScanOptions
{
    /// <summary>
    /// Gets the default scan options.
    /// </summary>
    public static UtilityCandidateScanOptions Default { get; } = new();

    /// <summary>
    /// Gets the optional source identity copied onto candidate and diagnostic spans.
    /// </summary>
    public string? SourceIdentity { get; init; }

    /// <summary>
    /// Gets the non-negative absolute offset added to every source-relative span.
    /// </summary>
    public int ContentOffset { get; init; }

    /// <summary>
    /// Gets the optional lowercase utility prefix expected by the candidate parser.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets the immutable variant registry used by the candidate parser.
    /// </summary>
    public UtilityVariantRegistry VariantRegistry { get; init; } =
        UtilityVariantRegistry.BuiltIn;
}
