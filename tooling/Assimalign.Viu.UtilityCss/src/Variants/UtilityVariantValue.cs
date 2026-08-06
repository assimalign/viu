namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An immutable value attached to a functional variant.
/// </summary>
/// <param name="Kind">The named, arbitrary, or CSS-variable syntax.</param>
/// <param name="Text">The decoded value. CSS-variable shorthand is represented as <c>var(...)</c>.</param>
/// <param name="RawText">The value exactly as authored.</param>
public sealed record UtilityVariantValue(
    UtilityValueKind Kind,
    string Text,
    string RawText);
