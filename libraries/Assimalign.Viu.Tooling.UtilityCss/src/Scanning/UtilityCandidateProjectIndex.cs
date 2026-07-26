using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Maintains the reference-counted union of literal utility candidates across project sources.
/// </summary>
/// <remarks>
/// This type models the single-threaded build and editor event loop. Occurrence-only changes and a
/// duplicate use in another source do not invalidate utility resolution. Removing the final source
/// reference removes the candidate deterministically.
/// </remarks>
public sealed class UtilityCandidateProjectIndex
{
    private readonly Dictionary<string, HashSet<string>> candidatesBySource =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> referenceCounts =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the distinct project candidates in ordinal order.
    /// </summary>
    public UtilityCollection<string> CandidateTexts =>
        new(
            referenceCounts.Keys.OrderBy(
                candidate => candidate,
                StringComparer.Ordinal));

    /// <summary>
    /// Gets the normalized source identities in ordinal order.
    /// </summary>
    public UtilityCollection<string> SourceIdentities =>
        new(
            candidatesBySource.Keys.OrderBy(
                sourceIdentity => sourceIdentity,
                StringComparer.Ordinal));

    /// <summary>
    /// Replaces one source snapshot and returns only first-reference and final-reference changes.
    /// </summary>
    /// <param name="sourceIdentity">The stable normalized project source identity.</param>
    /// <param name="scanResult">The complete latest scan result for that source.</param>
    /// <returns>The distinct project candidate change.</returns>
    public UtilityCandidateProjectChange UpdateSource(
        string sourceIdentity,
        UtilityCandidateScanResult scanResult)
    {
        if (string.IsNullOrWhiteSpace(sourceIdentity))
        {
            throw new ArgumentException(
                "A source identity is required.",
                nameof(sourceIdentity));
        }

        if (scanResult is null)
        {
            throw new ArgumentNullException(nameof(scanResult));
        }

        var nextCandidates = new HashSet<string>(
            scanResult.Candidates.Select(
                detection => detection.Candidate.RawText),
            StringComparer.Ordinal);
        candidatesBySource.TryGetValue(
            sourceIdentity,
            out var previousCandidates);
        previousCandidates ??= new HashSet<string>(StringComparer.Ordinal);

        var added = new List<string>();
        var removed = new List<string>();
        foreach (var candidate in previousCandidates)
        {
            if (nextCandidates.Contains(candidate))
            {
                continue;
            }

            if (referenceCounts[candidate] == 1)
            {
                referenceCounts.Remove(candidate);
                removed.Add(candidate);
            }
            else
            {
                referenceCounts[candidate]--;
            }
        }

        foreach (var candidate in nextCandidates)
        {
            if (previousCandidates.Contains(candidate))
            {
                continue;
            }

            if (referenceCounts.TryGetValue(candidate, out var count))
            {
                referenceCounts[candidate] = count + 1;
            }
            else
            {
                referenceCounts.Add(candidate, 1);
                added.Add(candidate);
            }
        }

        candidatesBySource[sourceIdentity] = nextCandidates;
        return CreateChange(added, removed);
    }

    /// <summary>
    /// Removes one source snapshot and returns candidates whose final reference disappeared.
    /// </summary>
    /// <param name="sourceIdentity">The stable normalized project source identity.</param>
    /// <returns>The distinct project candidate change.</returns>
    public UtilityCandidateProjectChange RemoveSource(string sourceIdentity)
    {
        if (string.IsNullOrWhiteSpace(sourceIdentity))
        {
            throw new ArgumentException(
                "A source identity is required.",
                nameof(sourceIdentity));
        }

        if (!candidatesBySource.TryGetValue(
                sourceIdentity,
                out var previousCandidates))
        {
            return CreateChange([], []);
        }

        candidatesBySource.Remove(sourceIdentity);
        var removed = new List<string>();
        foreach (var candidate in previousCandidates)
        {
            if (referenceCounts[candidate] == 1)
            {
                referenceCounts.Remove(candidate);
                removed.Add(candidate);
            }
            else
            {
                referenceCounts[candidate]--;
            }
        }

        return CreateChange([], removed);
    }

    private static UtilityCandidateProjectChange CreateChange(
        IEnumerable<string> added,
        IEnumerable<string> removed) =>
        new(
            new UtilityCollection<string>(
                added.OrderBy(
                    candidate => candidate,
                    StringComparer.Ordinal)),
            new UtilityCollection<string>(
                removed.OrderBy(
                    candidate => candidate,
                    StringComparer.Ordinal)));
}
