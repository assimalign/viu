using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class UtilityCssEditorCatalog
{
    internal const string FileName = "utilitycss.classcatalog.v1.json";

    internal static UtilityCssEditorCatalog Empty { get; } =
        new(Array.Empty<UtilityCssEditorCatalogEntry>(), false);

    private UtilityCssEditorCatalog(
        IReadOnlyList<UtilityCssEditorCatalogEntry> entries,
        bool isTruncated)
    {
        Entries = entries;
        IsTruncated = isTruncated;
    }

    internal IReadOnlyList<UtilityCssEditorCatalogEntry> Entries { get; }

    internal bool IsTruncated { get; }

    internal static UtilityCssEditorCatalog Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version) ||
                version != 1 ||
                !root.TryGetProperty("truncated", out var truncated) ||
                truncated.ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
                !root.TryGetProperty("entries", out var entriesElement) ||
                entriesElement.ValueKind != JsonValueKind.Array)
            {
                return Empty;
            }

            var entries = new List<UtilityCssEditorCatalogEntry>(
                entriesElement.GetArrayLength());
            var candidateTexts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entryElement in entriesElement.EnumerateArray())
            {
                if (!TryReadEntry(
                        entryElement,
                        entries.Count,
                        out var entry))
                {
                    return Empty;
                }

                if (candidateTexts.Add(entry.CandidateText))
                {
                    entries.Add(entry);
                }
            }

            return new UtilityCssEditorCatalog(
                entries,
                truncated.GetBoolean());
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException or
                  ArgumentException or
                  NotSupportedException)
        {
            return Empty;
        }
    }

    private static bool TryReadEntry(
        JsonElement element,
        int index,
        out UtilityCssEditorCatalogEntry entry)
    {
        entry = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("class", out var candidateTextElement) ||
            candidateTextElement.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("css", out var cssElement) ||
            cssElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidateText = candidateTextElement.GetString();
        var css = cssElement.GetString();
        if (string.IsNullOrEmpty(candidateText) || css is null)
        {
            return false;
        }

        string? colorValue = null;
        if (element.TryGetProperty("colorValue", out var colorValueElement))
        {
            if (colorValueElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            colorValue = colorValueElement.GetString();
            if (string.IsNullOrEmpty(colorValue))
            {
                return false;
            }
        }

        entry = new UtilityCssEditorCatalogEntry(
            candidateText,
            css,
            colorValue,
            index);
        return true;
    }
}
