using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Parses Viu Utilities v4.3.3 class candidates into immutable syntax models. Parsing is
/// deterministic, I/O-free, and recoverable: malformed author input is returned as diagnostics and
/// never thrown. A supplied <see cref="CancellationToken"/> is the sole expected exception path.
/// </summary>
public static class UtilityCandidateParser
{
    /// <summary>
    /// Parses <paramref name="rawText"/> with the built-in v4.3.3 variant registry and no configured
    /// prefix.
    /// </summary>
    /// <param name="rawText">The complete class candidate token.</param>
    /// <returns>The recoverable parse result.</returns>
    public static UtilityCandidateParseResult Parse(string? rawText) =>
        Parse(
            rawText,
            null,
            UtilityVariantRegistry.BuiltIn,
            CancellationToken.None);

    /// <summary>
    /// Parses <paramref name="rawText"/> with an optional configured prefix. When present, the prefix
    /// must be the first variant and remains the first item in the parsed variant collection.
    /// </summary>
    /// <param name="rawText">The complete class candidate token.</param>
    /// <param name="prefix">The optional lowercase configured prefix.</param>
    /// <returns>The recoverable parse result.</returns>
    public static UtilityCandidateParseResult Parse(
        string? rawText,
        string? prefix) =>
        Parse(
            rawText,
            prefix,
            UtilityVariantRegistry.BuiltIn,
            CancellationToken.None);

    /// <summary>
    /// Parses <paramref name="rawText"/> with an explicit immutable variant registry and cancellation
    /// boundary.
    /// </summary>
    /// <param name="rawText">The complete class candidate token.</param>
    /// <param name="prefix">The optional lowercase configured prefix.</param>
    /// <param name="variantRegistry">
    /// The registry used to classify static, functional, and compound variants.
    /// </param>
    /// <param name="cancellationToken">The cancellation boundary for editor and build hosts.</param>
    /// <returns>The recoverable parse result.</returns>
    /// <exception cref="System.OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityCandidateParseResult Parse(
        string? rawText,
        string? prefix,
        UtilityVariantRegistry variantRegistry,
        CancellationToken cancellationToken) =>
        UtilityCandidateParseEngine.Parse(
            rawText,
            prefix,
            variantRegistry ?? UtilityVariantRegistry.BuiltIn,
            cancellationToken);
}
