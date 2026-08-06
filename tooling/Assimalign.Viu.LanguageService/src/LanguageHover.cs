namespace Assimalign.Viu.LanguageService;

/// <summary>Markdown documentation for a token and the range it covers.</summary>
/// <param name="Markdown">The Markdown-formatted documentation.</param>
/// <param name="Range">The token range.</param>
public sealed record LanguageHover(string Markdown, LanguageRange Range);
