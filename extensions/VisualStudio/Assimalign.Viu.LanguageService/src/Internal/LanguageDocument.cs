using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

internal sealed class LanguageDocument
{
    private LanguageDocument(
        string documentUri,
        string text,
        int? version,
        SingleFileComponentParseResult parseResult)
    {
        DocumentUri = documentUri;
        Text = text;
        Version = version;
        ParseResult = parseResult;
    }

    internal string DocumentUri { get; }

    internal string Text { get; }

    internal int? Version { get; }

    internal SingleFileComponentParseResult ParseResult { get; }

    internal static LanguageDocument Create(string documentUri, string text, int? version)
        => new(documentUri, text, version, SingleFileComponentParser.Parse(text));
}
