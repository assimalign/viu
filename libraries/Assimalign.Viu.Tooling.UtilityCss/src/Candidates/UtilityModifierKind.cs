namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies the syntax used for a slash modifier.
/// </summary>
public enum UtilityModifierKind
{
    /// <summary>A named modifier such as <c>50</c> in <c>bg-red-500/50</c>.</summary>
    Named,

    /// <summary>An arbitrary modifier such as <c>[50%]</c>.</summary>
    Arbitrary,

    /// <summary>A CSS-variable parenthesis shorthand such as <c>(--opacity)</c>.</summary>
    CssVariable,
}
