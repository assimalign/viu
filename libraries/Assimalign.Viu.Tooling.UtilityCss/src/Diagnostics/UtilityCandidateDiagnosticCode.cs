namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies a recoverable utility candidate parser diagnostic.
/// </summary>
public enum UtilityCandidateDiagnosticCode
{
    /// <summary>No candidate text was supplied.</summary>
    EmptyCandidate,

    /// <summary>A bracket, parenthesis, brace, quote, or escape is incomplete or mismatched.</summary>
    UnbalancedDelimiter,

    /// <summary>A variant or utility segment is empty.</summary>
    EmptySegment,

    /// <summary>The configured prefix is absent or is not the first variant.</summary>
    PrefixMismatch,

    /// <summary>A variant root is not present in the v4.3.3 registry.</summary>
    UnknownVariant,

    /// <summary>A known variant uses a grammar shape that its definition does not accept.</summary>
    InvalidVariant,

    /// <summary>The important marker is empty, duplicated, or otherwise malformed.</summary>
    InvalidImportantMarker,

    /// <summary>The accepted deprecated leading important marker was used.</summary>
    DeprecatedLeadingImportantMarker,

    /// <summary>The negative marker is not followed by a utility.</summary>
    InvalidNegativeForm,

    /// <summary>A slash modifier is empty, malformed, or appears more than once at top level.</summary>
    InvalidModifier,

    /// <summary>An arbitrary value is empty or structurally invalid.</summary>
    InvalidArbitraryValue,

    /// <summary>An arbitrary CSS property is malformed.</summary>
    InvalidArbitraryProperty,

    /// <summary>The utility root or named value is malformed.</summary>
    InvalidUtility,
}
