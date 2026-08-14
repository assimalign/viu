using System;
using System.Collections.Generic;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class UtilityCssLanguageDocumentStore
{
    private readonly Dictionary<string, UtilityCssLanguageDocument> documents =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Open(string documentUri, string text, int? version)
        => documents[documentUri] = UtilityCssLanguageDocument.Create(
            documentUri,
            text,
            version);

    internal bool Change(string documentUri, string text, int? version)
    {
        if (!documents.TryGetValue(documentUri, out var document) ||
            IsStaleVersion(document.Version, version))
        {
            return false;
        }

        documents[documentUri] = UtilityCssLanguageDocument.Create(
            documentUri,
            text,
            version);
        return true;
    }

    internal bool Close(string documentUri) => documents.Remove(documentUri);

    internal bool TryGet(
        string documentUri,
        out UtilityCssLanguageDocument document)
        => documents.TryGetValue(documentUri, out document!);

    private static bool IsStaleVersion(int? currentVersion, int? nextVersion)
        => currentVersion.HasValue &&
           nextVersion.HasValue &&
           nextVersion.Value <= currentVersion.Value;
}
