namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// A foldable line span in a Viu single-file-component document. The range covers a block's content
/// while leaving its closing delimiter line visible.
/// </summary>
/// <param name="StartLine">The zero-based line where the fold starts.</param>
/// <param name="EndLine">The zero-based line where the fold ends, inclusive.</param>
/// <param name="Kind">The fold kind, or <see langword="null"/> for an unclassified fold.</param>
public sealed record LanguageFoldingRange(int StartLine, int EndLine, string? Kind = null);
