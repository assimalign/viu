namespace Assimalign.Viu.LanguageService;

/// <summary>A half-open range in an editor document.</summary>
/// <param name="Start">The inclusive range start.</param>
/// <param name="End">The exclusive range end.</param>
public readonly record struct LanguageRange(LanguagePosition Start, LanguagePosition End);
