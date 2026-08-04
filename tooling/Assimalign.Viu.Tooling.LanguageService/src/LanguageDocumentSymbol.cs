using System.Collections.Generic;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>A node in the hierarchical outline of a Viu single-file-component document.</summary>
/// <param name="Name">The symbol name as authored, such as <c>&lt;template&gt;</c> or <c>@script</c>.</param>
/// <param name="Detail">The block options as authored, or <see langword="null"/> when the header has none.</param>
/// <param name="Kind">The semantic kind of the symbol.</param>
/// <param name="Range">The full range of the symbol, including its container delimiters.</param>
/// <param name="SelectionRange">
/// The header name token to select when the symbol is revealed. Always contained in
/// <paramref name="Range"/>, matching the Language Server Protocol invariant.
/// </param>
/// <param name="Children">The symbols nested inside this symbol, in source order.</param>
public sealed record LanguageDocumentSymbol(
    string Name,
    string? Detail,
    LanguageSymbolKind Kind,
    LanguageRange Range,
    LanguageRange SelectionRange,
    IReadOnlyList<LanguageDocumentSymbol> Children);
