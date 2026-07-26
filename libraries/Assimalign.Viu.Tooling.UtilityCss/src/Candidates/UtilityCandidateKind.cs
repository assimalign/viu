namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies the top-level utility candidate form.
/// </summary>
public enum UtilityCandidateKind
{
    /// <summary>A named utility, with either a named, arbitrary, variable, or no value.</summary>
    Named,

    /// <summary>An arbitrary CSS property such as <c>[mask-type:luminance]</c>.</summary>
    ArbitraryProperty,
}
