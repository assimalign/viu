namespace Assimalign.Viu.LanguageService;

/// <summary>
/// A full-document replacement or incremental editor change.
/// </summary>
/// <param name="Range">
/// The range to replace, or <see langword="null"/> when <paramref name="Text"/> replaces the complete document.
/// </param>
/// <param name="Text">The replacement text.</param>
public sealed record LanguageDocumentChange(LanguageRange? Range, string Text);
