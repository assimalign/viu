namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The immutable, value-equatable structure of one utility-class candidate. This syntax model does
/// not decide whether a utility family supports negatives, fractions, or modifiers; later registry
/// and resolver stages validate those capabilities.
/// </summary>
/// <param name="RawText">The candidate exactly as authored.</param>
/// <param name="CanonicalText">
/// The canonical spelling. In particular, an accepted deprecated leading important marker is moved
/// to the v4 trailing position.
/// </param>
/// <param name="Kind">The top-level candidate form.</param>
/// <param name="Root">
/// The named utility root, or the CSS property name for an arbitrary-property candidate.
/// </param>
/// <param name="UtilityText">
/// The utility portion after variant and important-marker removal but before structural parsing.
/// </param>
/// <param name="Value">The optional named, arbitrary, or CSS-variable value.</param>
/// <param name="Modifier">The optional slash modifier.</param>
/// <param name="IsNegative">
/// Whether the utility used the leading negative form. Capability validation is deferred.
/// </param>
/// <param name="Variants">
/// Variants in authored left-to-right order. A configured prefix is represented as the first item.
/// </param>
/// <param name="ImportantMarker">The important marker syntax used by the author.</param>
public sealed record UtilityCandidate(
    string RawText,
    string CanonicalText,
    UtilityCandidateKind Kind,
    string Root,
    string UtilityText,
    UtilityValue? Value,
    UtilityModifier? Modifier,
    bool IsNegative,
    UtilityCollection<UtilityVariant> Variants,
    UtilityImportantMarker ImportantMarker)
{
    /// <summary>
    /// Gets whether the candidate carries either accepted important marker.
    /// </summary>
    public bool IsImportant => ImportantMarker != UtilityImportantMarker.None;

    /// <summary>
    /// Gets whether the accepted important marker uses deprecated leading syntax.
    /// </summary>
    public bool UsesDeprecatedImportantMarker =>
        ImportantMarker == UtilityImportantMarker.DeprecatedLeading;
}
