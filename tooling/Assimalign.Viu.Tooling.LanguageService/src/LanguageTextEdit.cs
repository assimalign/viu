namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>A single text replacement applied to an editor document.</summary>
/// <param name="Range">The range replaced by the edit; a zero-width range inserts.</param>
/// <param name="NewText">The text inserted in place of the range.</param>
public sealed record LanguageTextEdit(LanguageRange Range, string NewText);
