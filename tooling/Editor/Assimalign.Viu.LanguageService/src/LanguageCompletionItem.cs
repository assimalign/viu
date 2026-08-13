namespace Assimalign.Viu.LanguageService;

/// <summary>A context-sensitive completion offered for a Viu single-file-component document.</summary>
/// <param name="Label">The text shown in the completion list.</param>
/// <param name="Kind">The semantic kind of the item.</param>
/// <param name="Detail">A compact signature or category description.</param>
/// <param name="Documentation">Markdown documentation for the item.</param>
/// <param name="InsertText">The text or snippet inserted when the item is committed.</param>
/// <param name="IsSnippet">Whether <paramref name="InsertText"/> uses snippet placeholders.</param>
/// <param name="SortText">The stable key used to order the item.</param>
/// <param name="EditRange">
/// The document range replaced when the item is committed, or <see langword="null"/> to use the editor's
/// default insertion range. Utility-class completion supplies this range so committing a suggestion
/// replaces the complete partially typed candidate, including variant and modifier punctuation.
/// </param>
/// <param name="FilterText">
/// The text the editor uses to filter the item, or <see langword="null"/> to filter by
/// <paramref name="Label"/>.
/// </param>
/// <param name="ColorValue">
/// The computed CSS color represented by this completion, or <see langword="null"/> when the item
/// has no color presentation. The value is carried independently of documentation so language
/// clients can render a swatch without parsing a Markdown code block. [V01.01.12.07.13]
/// </param>
public sealed record LanguageCompletionItem(
    string Label,
    LanguageCompletionItemKind Kind,
    string Detail,
    string Documentation,
    string InsertText,
    bool IsSnippet,
    string SortText,
    LanguageRange? EditRange = null,
    string? FilterText = null,
    string? ColorValue = null);
