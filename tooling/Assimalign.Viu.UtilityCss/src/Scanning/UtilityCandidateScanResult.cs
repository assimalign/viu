using System.Linq;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The deterministic, structurally value-equatable result of scanning one supplied markup source.
/// </summary>
/// <param name="Candidates">
/// Distinct candidates sorted by exact authored text. Each entry retains all occurrence spans.
/// </param>
/// <param name="Diagnostics">Recoverable diagnostics in stable source order.</param>
public sealed record UtilityCandidateScanResult(
    UtilityCollection<UtilityCandidateDetection> Candidates,
    UtilityCollection<UtilityCandidateScanDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether no error diagnostic was produced.
    /// </summary>
    public bool IsSuccess =>
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityCandidateDiagnosticSeverity.Error);
}
