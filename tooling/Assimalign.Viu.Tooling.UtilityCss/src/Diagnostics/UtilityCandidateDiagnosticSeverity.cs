namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies whether a candidate diagnostic prevents a structured candidate.
/// </summary>
public enum UtilityCandidateDiagnosticSeverity
{
    /// <summary>The candidate remains usable but authored deprecated compatibility syntax.</summary>
    Warning,

    /// <summary>The candidate is malformed and no structured candidate is returned.</summary>
    Error,
}
