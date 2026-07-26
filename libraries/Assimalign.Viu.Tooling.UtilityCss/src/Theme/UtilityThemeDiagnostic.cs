namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// A recoverable CSS-first theme diagnostic mapped to the supplied source.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">Whether the affected input is ignored.</param>
/// <param name="Message">A concise author-facing explanation.</param>
/// <param name="SourceSpan">The exact absolute source span.</param>
public sealed record UtilityThemeDiagnostic(
    UtilityThemeDiagnosticCode Code,
    UtilityThemeDiagnosticSeverity Severity,
    string Message,
    UtilityThemeSourceSpan SourceSpan);
