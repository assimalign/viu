namespace Assimalign.Viu.LanguageService;

/// <summary>
/// A zero-based line and UTF-16 character position in an editor document.
/// </summary>
/// <param name="Line">The zero-based line.</param>
/// <param name="Character">The zero-based UTF-16 character offset on the line.</param>
public readonly record struct LanguagePosition(int Line, int Character);
