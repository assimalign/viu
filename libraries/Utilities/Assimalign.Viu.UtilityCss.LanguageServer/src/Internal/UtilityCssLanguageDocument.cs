namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class UtilityCssLanguageDocument
{
    private UtilityCssLanguageDocument(
        string documentUri,
        string text,
        int? version,
        UtilityCssDocumentRegion? region)
    {
        DocumentUri = documentUri;
        Text = text;
        Version = version;
        Region = region;
    }

    internal string DocumentUri { get; }

    internal string Text { get; }

    internal int? Version { get; }

    internal UtilityCssDocumentRegion? Region { get; }

    internal static UtilityCssLanguageDocument Create(
        string documentUri,
        string text,
        int? version)
        => new(
            documentUri,
            text,
            version,
            UtilityCssDocumentRegion.TryCreate(documentUri, text, out var region)
                ? region
                : null);
}
