using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The bounded completion items for one request and whether a narrower request may reveal more.
/// [V01.01.12.30]
/// </summary>
/// <param name="Items">The completion items, already bounded by the language-service limit.</param>
/// <param name="IsIncomplete">
/// Whether the source set or the merged result was truncated. A host transports this bit so the
/// client re-requests completion as the author types a narrower prefix.
/// </param>
public sealed record LanguageCompletionList(
    IReadOnlyList<LanguageCompletionItem> Items,
    bool IsIncomplete);
