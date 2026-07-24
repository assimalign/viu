using System;
using System.Collections.Generic;

namespace Assimalign.Viu.LanguageService;

internal sealed class LanguageDocumentStore
{
    private readonly Dictionary<string, LanguageDocument> documents =
        new(StringComparer.Ordinal);

    internal void Open(string documentUri, string text, int? version)
        => documents[documentUri] = LanguageDocument.Create(documentUri, text, version);

    internal bool Change(
        string documentUri,
        int? version,
        IReadOnlyList<LanguageDocumentChange> changes)
    {
        if (!documents.TryGetValue(documentUri, out var document) ||
            IsStaleVersion(document.Version, version))
        {
            return false;
        }

        var text = document.Text;
        foreach (var change in changes)
        {
            if (change.Range is null)
            {
                text = change.Text;
                continue;
            }

            if (!TextCoordinateConverter.TryGetOffset(text, change.Range.Value.Start, out var start) ||
                !TextCoordinateConverter.TryGetOffset(text, change.Range.Value.End, out var end) ||
                end < start)
            {
                return false;
            }

            text = string.Concat(text.AsSpan(0, start), change.Text, text.AsSpan(end));
        }

        documents[documentUri] = LanguageDocument.Create(documentUri, text, version);
        return true;
    }

    internal bool Close(string documentUri) => documents.Remove(documentUri);

    internal bool TryGet(string documentUri, out LanguageDocument document)
        => documents.TryGetValue(documentUri, out document!);

    private static bool IsStaleVersion(int? currentVersion, int? nextVersion)
        => currentVersion.HasValue &&
           nextVersion.HasValue &&
           nextVersion.Value <= currentVersion.Value;
}
