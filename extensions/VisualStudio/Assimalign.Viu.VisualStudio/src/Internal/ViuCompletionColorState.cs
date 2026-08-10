using System;
using System.Collections.Generic;
using System.IO;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Stores a bounded queue of color-bearing completion responses for each open document.
/// </summary>
internal sealed class ViuCompletionColorState
{
    private readonly object gate = new();
    private const int MaximumPublicationsPerDocument = 8;

    private readonly Dictionary<string, List<ViuCompletionColorPublication>> publicationsByDocument =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the process-wide state shared by the client and completion adapter.</summary>
    internal static ViuCompletionColorState Shared { get; } = new();

    /// <summary>Queues one completed server response until its exact source list is presented.</summary>
    internal void Publish(ViuCompletionColorPublication publication)
    {
        if (publication is null)
        {
            throw new ArgumentNullException(nameof(publication));
        }

        string key = NormalizeDocumentKey(publication.DocumentUri);
        lock (this.gate)
        {
            if (!this.publicationsByDocument.TryGetValue(key, out var publications))
            {
                publications = new List<ViuCompletionColorPublication>();
                this.publicationsByDocument.Add(key, publications);
            }

            publications.Add(publication);
            if (publications.Count > MaximumPublicationsPerDocument)
            {
                publications.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Consumes the newest response that exactly matches one and only one completion-source group.
    /// </summary>
    internal bool TryTakeMatchingPublication(
        string documentPath,
        IReadOnlyList<IReadOnlyList<ViuCompletionCandidateIdentity>> sourceGroups,
        out int sourceGroupIndex,
        out ViuCompletionColorPublication? publication)
    {
        sourceGroupIndex = -1;
        publication = null;
        if (string.IsNullOrWhiteSpace(documentPath) || sourceGroups.Count == 0)
        {
            return false;
        }

        string key = NormalizeDocumentKey(documentPath);
        lock (this.gate)
        {
            if (!this.publicationsByDocument.TryGetValue(key, out var publications))
            {
                return false;
            }

            for (int publicationIndex = publications.Count - 1;
                 publicationIndex >= 0;
                 publicationIndex--)
            {
                ViuCompletionColorPublication candidate = publications[publicationIndex];
                int matchingGroupIndex = -1;
                int matchingGroupCount = 0;
                for (var groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
                {
                    if (!candidate.Matches(sourceGroups[groupIndex]))
                    {
                        continue;
                    }

                    matchingGroupIndex = groupIndex;
                    matchingGroupCount++;
                }

                if (matchingGroupCount == 0)
                {
                    continue;
                }

                publications.RemoveAt(publicationIndex);
                if (publications.Count == 0)
                {
                    this.publicationsByDocument.Remove(key);
                }

                if (matchingGroupCount != 1)
                {
                    return false;
                }

                sourceGroupIndex = matchingGroupIndex;
                publication = candidate;
                return true;
            }

            return false;
        }
    }

    /// <summary>Clears response state when the language-server session is replaced.</summary>
    internal void Clear()
    {
        lock (this.gate)
        {
            this.publicationsByDocument.Clear();
        }
    }

    private static string NormalizeDocumentKey(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        {
            return Path.GetFullPath(uri.LocalPath);
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return value;
        }
    }
}
