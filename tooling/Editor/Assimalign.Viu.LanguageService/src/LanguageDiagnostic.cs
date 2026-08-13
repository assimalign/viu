namespace Assimalign.Viu.LanguageService;

/// <summary>An editor-neutral diagnostic produced for a <c>.viu</c> document.</summary>
/// <param name="Range">The source range associated with the diagnostic.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Code">The stable Viu diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic message.</param>
/// <param name="Source">The subsystem that produced the diagnostic.</param>
public sealed record LanguageDiagnostic(
    LanguageRange Range,
    LanguageDiagnosticSeverity Severity,
    string Code,
    string Message,
    string Source);
