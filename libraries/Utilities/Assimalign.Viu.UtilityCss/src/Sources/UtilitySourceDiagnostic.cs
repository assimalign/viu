namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// A recoverable CSS-first source-configuration diagnostic mapped to its authored source.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">Whether the affected construct remains usable.</param>
/// <param name="Message">A concise author-facing explanation.</param>
/// <param name="SourceSpan">The exact affected source span.</param>
public sealed record UtilitySourceDiagnostic(
    UtilitySourceDiagnosticCode Code,
    UtilitySourceDiagnosticSeverity Severity,
    string Message,
    UtilityStylesheetSourceSpan SourceSpan);
