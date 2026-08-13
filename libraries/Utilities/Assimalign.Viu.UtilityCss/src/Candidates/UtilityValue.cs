namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An immutable value attached to a named utility.
/// </summary>
/// <param name="Kind">The authored value syntax.</param>
/// <param name="Text">The decoded value. CSS-variable shorthand is represented as <c>var(...)</c>.</param>
/// <param name="RawText">The value text exactly as authored, including brackets or parentheses.</param>
/// <param name="DataType">An optional arbitrary-value type hint such as <c>color</c>.</param>
/// <param name="Fraction">
/// The preserved named fraction, such as <c>1/2</c>. It remains structural data until a utility
/// family registry decides whether that family supports fractions.
/// </param>
public sealed record UtilityValue(
    UtilityValueKind Kind,
    string Text,
    string RawText,
    string? DataType,
    string? Fraction);
