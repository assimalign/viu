namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// An immutable slash modifier attached to a utility or variant.
/// </summary>
/// <param name="Kind">The authored modifier syntax.</param>
/// <param name="Text">The decoded modifier. CSS-variable shorthand is represented as <c>var(...)</c>.</param>
/// <param name="RawText">The modifier text exactly as authored, excluding the slash.</param>
public sealed record UtilityModifier(
    UtilityModifierKind Kind,
    string Text,
    string RawText);
