namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// A recoverable diagnostic shared by compiler and editor hosts.
/// </summary>
/// <param name="CandidateText">The complete candidate that produced the diagnostic.</param>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">Whether CSS generation can continue for the candidate.</param>
/// <param name="Message">The author-facing explanation.</param>
public sealed record UtilityCssDiagnostic(
    string CandidateText,
    UtilityCssDiagnosticCode Code,
    UtilityCssDiagnosticSeverity Severity,
    string Message);
