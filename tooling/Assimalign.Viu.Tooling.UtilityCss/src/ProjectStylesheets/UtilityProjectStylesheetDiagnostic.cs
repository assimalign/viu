namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// One source-located semantic project-stylesheet diagnostic.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Message">The actionable diagnostic message.</param>
/// <param name="SourceSpan">The exact source span that caused the diagnostic.</param>
public sealed record UtilityProjectStylesheetDiagnostic(
    UtilityProjectStylesheetDiagnosticCode Code,
    UtilityProjectStylesheetDiagnosticSeverity Severity,
    string Message,
    UtilityStylesheetSourceSpan SourceSpan);
