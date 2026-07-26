namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// A recoverable scanner diagnostic mapped to the supplied source text.
/// </summary>
/// <param name="Code">The scanner diagnostic category.</param>
/// <param name="Severity">Whether the affected candidate can be used.</param>
/// <param name="Message">A concise author-facing explanation.</param>
/// <param name="SourceSpan">The exact absolute source span.</param>
/// <param name="CandidateDiagnosticCode">
/// The underlying candidate parser code for
/// <see cref="UtilityCandidateScanDiagnosticCode.CandidateParserDiagnostic"/>; otherwise
/// <see langword="null"/>.
/// </param>
public sealed record UtilityCandidateScanDiagnostic(
    UtilityCandidateScanDiagnosticCode Code,
    UtilityCandidateDiagnosticSeverity Severity,
    string Message,
    UtilityCandidateSourceSpan SourceSpan,
    UtilityCandidateDiagnosticCode? CandidateDiagnosticCode);
