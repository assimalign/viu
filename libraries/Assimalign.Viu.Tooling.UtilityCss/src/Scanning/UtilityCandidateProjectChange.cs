namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Describes the distinct project candidate changes caused by updating or removing one source.
/// </summary>
/// <param name="AddedCandidates">
/// Candidates whose first project reference was introduced.
/// </param>
/// <param name="RemovedCandidates">
/// Candidates whose final project reference was removed.
/// </param>
public sealed record UtilityCandidateProjectChange(
    UtilityCollection<string> AddedCandidates,
    UtilityCollection<string> RemovedCandidates)
{
    /// <summary>
    /// Gets whether the distinct project candidate set changed.
    /// </summary>
    public bool IsChanged =>
        AddedCandidates.Count != 0 ||
        RemovedCandidates.Count != 0;
}
