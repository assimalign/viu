using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The recoverable result of parsing one utility-class candidate.
/// </summary>
/// <param name="Candidate">
/// The immutable candidate when no error prevented structural parsing; otherwise
/// <see langword="null"/>.
/// </param>
/// <param name="Diagnostics">Warnings and errors in stable source order.</param>
public sealed record UtilityCandidateParseResult(
    UtilityCandidate? Candidate,
    UtilityCollection<UtilityCandidateDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether a candidate was produced and no error diagnostic is present.
    /// </summary>
    public bool IsSuccess =>
        Candidate is not null &&
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityCandidateDiagnosticSeverity.Error);
}
