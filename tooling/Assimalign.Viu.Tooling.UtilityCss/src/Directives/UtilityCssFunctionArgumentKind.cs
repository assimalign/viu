namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Classifies one structurally parsed CSS-first function argument.
/// </summary>
public enum UtilityCssFunctionArgumentKind
{
    /// <summary>A theme custom-property pattern such as <c>--text-*</c>.</summary>
    Theme,

    /// <summary>A documented bare data type such as <c>integer</c> or <c>ratio</c>.</summary>
    Bare,

    /// <summary>A quoted literal candidate value.</summary>
    Literal,

    /// <summary>An arbitrary-value data type enclosed in square brackets.</summary>
    Arbitrary,

    /// <summary>A nested <c>--default(...)</c> fallback.</summary>
    Default,

    /// <summary>A balanced CSS expression consumed by <c>--spacing()</c> or <c>--alpha()</c>.</summary>
    Expression,
}
