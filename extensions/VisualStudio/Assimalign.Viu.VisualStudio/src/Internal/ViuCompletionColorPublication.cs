using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Represents the color payloads from one Language Server Protocol completion response.
/// </summary>
internal sealed class ViuCompletionColorPublication
{
    private readonly IReadOnlyDictionary<ViuCompletionCandidateIdentity, int> candidateCounts;

    private ViuCompletionColorPublication(
        string documentUri,
        IReadOnlyList<ViuCompletionCandidateIdentity> candidates,
        IReadOnlyDictionary<ViuCompletionCandidateIdentity, ViuCompletionColorPresentation>
            presentations)
    {
        this.DocumentUri = documentUri;
        this.Candidates = candidates;
        this.Presentations = presentations;
        this.candidateCounts = CountCandidates(candidates);
    }

    /// <summary>Gets the document URI from the completion request.</summary>
    internal string DocumentUri { get; }

    /// <summary>Gets every candidate identity from the same completion response.</summary>
    internal IReadOnlyList<ViuCompletionCandidateIdentity> Candidates { get; }

    /// <summary>Gets parsed color presentations keyed by complete candidate identity.</summary>
    internal IReadOnlyDictionary<ViuCompletionCandidateIdentity, ViuCompletionColorPresentation>
        Presentations { get; }

    /// <summary>
    /// Reads the request document and the result returned by <c>textDocument/completion</c>.
    /// </summary>
    internal static bool TryParse(
        JsonElement methodParameters,
        JsonElement response,
        out ViuCompletionColorPublication? publication)
    {
        publication = null;
        if (!TryGetDocumentUri(methodParameters, out string? documentUri))
        {
            return false;
        }

        var candidates = new List<ViuCompletionCandidateIdentity>();
        var presentations =
            new Dictionary<ViuCompletionCandidateIdentity, ViuCompletionColorPresentation>();
        if (TryGetItems(response, out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (!TryGetCandidateIdentity(item, out ViuCompletionCandidateIdentity identity))
                {
                    return false;
                }

                candidates.Add(identity);
                if (!item.TryGetProperty("data", out JsonElement dataElement) ||
                    dataElement.ValueKind != JsonValueKind.Object ||
                    !dataElement.TryGetProperty("colorValue", out JsonElement valueElement) ||
                    valueElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? value = valueElement.GetString();
                if (ViuCompletionColor.TryParse(value, out ViuCompletionColor color))
                {
                    presentations[identity] = new ViuCompletionColorPresentation(value!, color);
                }
            }
        }

        publication = new ViuCompletionColorPublication(documentUri!, candidates, presentations);
        return true;
    }

    /// <summary>Determines whether a source group is exactly this response's candidate multiset.</summary>
    internal bool Matches(IReadOnlyList<ViuCompletionCandidateIdentity> candidates)
    {
        if (candidates.Count != this.Candidates.Count)
        {
            return false;
        }

        IReadOnlyDictionary<ViuCompletionCandidateIdentity, int> counts =
            CountCandidates(candidates);
        if (counts.Count != this.candidateCounts.Count)
        {
            return false;
        }

        foreach (KeyValuePair<ViuCompletionCandidateIdentity, int> candidate in
                 this.candidateCounts)
        {
            if (!counts.TryGetValue(candidate.Key, out int count) || count != candidate.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<ViuCompletionCandidateIdentity, int> CountCandidates(
        IReadOnlyList<ViuCompletionCandidateIdentity> candidates)
    {
        var counts = new Dictionary<ViuCompletionCandidateIdentity, int>();
        foreach (ViuCompletionCandidateIdentity candidate in candidates)
        {
            counts.TryGetValue(candidate, out int count);
            counts[candidate] = count + 1;
        }

        return counts;
    }

    private static bool TryGetCandidateIdentity(
        JsonElement item,
        out ViuCompletionCandidateIdentity identity)
    {
        identity = default;
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("label", out JsonElement labelElement) ||
            labelElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(labelElement.GetString()))
        {
            return false;
        }

        string label = labelElement.GetString()!;
        string insertText = GetString(item, "insertText") ?? label;
        if (item.TryGetProperty("textEdit", out JsonElement textEdit) &&
            textEdit.ValueKind == JsonValueKind.Object)
        {
            insertText = GetString(textEdit, "newText") ?? insertText;
        }

        identity = new ViuCompletionCandidateIdentity(
            label,
            insertText,
            GetString(item, "sortText") ?? label,
            GetString(item, "filterText") ?? label);
        return true;
    }

    private static string? GetString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetDocumentUri(JsonElement methodParameters, out string? documentUri)
    {
        documentUri = null;
        return methodParameters.ValueKind == JsonValueKind.Object &&
            methodParameters.TryGetProperty("textDocument", out JsonElement document) &&
            document.ValueKind == JsonValueKind.Object &&
            document.TryGetProperty("uri", out JsonElement uri) &&
            uri.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(documentUri = uri.GetString());
    }

    private static bool TryGetItems(JsonElement response, out JsonElement items)
    {
        items = default;
        if (response.ValueKind == JsonValueKind.Array)
        {
            items = response;
            return true;
        }

        if (response.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (response.TryGetProperty("items", out items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        // Keeping this envelope form makes the parser independently testable with a raw JSON-RPC
        // response as well as with the result-only JsonDocument supplied by Visual Studio's middle
        // layer contract.
        return response.TryGetProperty("result", out JsonElement result) &&
            TryGetItems(result, out items);
    }
}
