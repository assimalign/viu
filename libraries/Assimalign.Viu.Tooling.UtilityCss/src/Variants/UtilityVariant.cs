namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One immutable variant in a parsed candidate.
/// </summary>
/// <param name="Kind">The variant grammar shape.</param>
/// <param name="Category">The built-in variant family.</param>
/// <param name="Root">The prefix or built-in variant root; empty for an arbitrary variant.</param>
/// <param name="RawText">The variant exactly as authored, excluding the separating colon.</param>
/// <param name="SourceOrder">
/// The zero-based, left-to-right authored position. A configured prefix occupies position zero.
/// A nested compound variant inherits its containing variant's position.
/// </param>
/// <param name="Value">The optional functional-variant value.</param>
/// <param name="Modifier">The optional slash modifier.</param>
/// <param name="NestedVariant">The variant wrapped by a compound variant.</param>
/// <param name="Selector">The decoded and normalized selector or at-rule for an arbitrary variant.</param>
/// <param name="IsRelativeSelector">
/// Whether an arbitrary selector begins with a relative combinator (<c>&gt;</c>, <c>+</c>, or <c>~</c>).
/// </param>
public sealed record UtilityVariant(
    UtilityVariantKind Kind,
    UtilityVariantCategory Category,
    string Root,
    string RawText,
    int SourceOrder,
    UtilityVariantValue? Value,
    UtilityModifier? Modifier,
    UtilityVariant? NestedVariant,
    string? Selector,
    bool IsRelativeSelector);
