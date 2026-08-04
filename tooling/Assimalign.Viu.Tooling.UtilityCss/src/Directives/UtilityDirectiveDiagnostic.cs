namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// A recoverable CSS-first directive diagnostic mapped to the supplied source.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">Whether the affected construct remains usable.</param>
/// <param name="Message">A concise author-facing explanation.</param>
/// <param name="SourceSpan">The exact absolute source span.</param>
public sealed record UtilityDirectiveDiagnostic(
    UtilityDirectiveDiagnosticCode Code,
    UtilityDirectiveDiagnosticSeverity Severity,
    string Message,
    UtilityStylesheetSourceSpan SourceSpan);
