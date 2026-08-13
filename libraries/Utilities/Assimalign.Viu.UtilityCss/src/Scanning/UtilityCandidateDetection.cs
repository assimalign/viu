namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One distinct parsed candidate and every occurrence of its exact authored spelling in a source.
/// </summary>
/// <param name="Candidate">The parsed utility candidate.</param>
/// <param name="SourceSpans">
/// Occurrence spans in ascending source order. Duplicate authored candidates share this entry.
/// </param>
public sealed record UtilityCandidateDetection(
    UtilityCandidate Candidate,
    UtilityCollection<UtilityCandidateSourceSpan> SourceSpans);
