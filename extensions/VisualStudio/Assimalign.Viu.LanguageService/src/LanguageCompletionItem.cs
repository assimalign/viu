namespace Assimalign.Viu.LanguageService;

/// <summary>A context-sensitive completion offered for a <c>.viu</c> document.</summary>
/// <param name="Label">The text shown in the completion list.</param>
/// <param name="Kind">The semantic kind of the item.</param>
/// <param name="Detail">A compact signature or category description.</param>
/// <param name="Documentation">Markdown documentation for the item.</param>
/// <param name="InsertText">The text or snippet inserted when the item is committed.</param>
/// <param name="IsSnippet">Whether <paramref name="InsertText"/> uses snippet placeholders.</param>
/// <param name="SortText">The stable key used to order the item.</param>
public sealed record LanguageCompletionItem(
    string Label,
    LanguageCompletionItemKind Kind,
    string Detail,
    string Documentation,
    string InsertText,
    bool IsSnippet,
    string SortText);
