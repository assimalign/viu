namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies a recoverable utility candidate scanner diagnostic.
/// </summary>
public enum UtilityCandidateScanDiagnosticCode
{
    /// <summary>A candidate parser diagnostic was mapped onto its source span.</summary>
    CandidateParserDiagnostic,

    /// <summary>A runtime interpolation or concatenation fragment cannot be a literal candidate.</summary>
    DynamicInterpolation,
}
