namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One executable selector or slot-form <c>@custom-variant</c> definition.
/// </summary>
/// <param name="Name">The exact variant name.</param>
/// <param name="Selector">
/// The shorthand selector without its outer parentheses, or <see langword="null"/> for slot form.
/// </param>
/// <param name="Body">The slot-form body, or <see langword="null"/> for shorthand form.</param>
/// <param name="SourceSpan">The complete definition source span.</param>
/// <param name="IsReferenced">
/// Whether the definition came through <c>@reference</c> and therefore contributes no direct output.
/// </param>
public sealed record UtilityCustomVariantDefinition(
    string Name,
    string? Selector,
    string? Body,
    UtilityStylesheetSourceSpan SourceSpan,
    bool IsReferenced);
