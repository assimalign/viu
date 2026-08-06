namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// A recoverable candidate parser diagnostic positioned in the authored class token.
/// </summary>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">Whether parsing can still return a candidate.</param>
/// <param name="Message">A concise author-facing explanation.</param>
/// <param name="Start">The zero-based start offset in the candidate token.</param>
/// <param name="Length">The affected character count.</param>
public sealed record UtilityCandidateDiagnostic(
    UtilityCandidateDiagnosticCode Code,
    UtilityCandidateDiagnosticSeverity Severity,
    string Message,
    int Start,
    int Length);
