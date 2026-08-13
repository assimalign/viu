namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies the syntax used for a utility value.
/// </summary>
public enum UtilityValueKind
{
    /// <summary>A named theme or bare value such as <c>blue-500</c> or <c>4</c>.</summary>
    Named,

    /// <summary>An arbitrary bracket value such as <c>[32px]</c>.</summary>
    Arbitrary,

    /// <summary>A CSS-variable parenthesis shorthand such as <c>(--brand)</c>.</summary>
    CssVariable,
}
