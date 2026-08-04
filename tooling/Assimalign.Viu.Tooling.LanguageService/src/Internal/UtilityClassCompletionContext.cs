using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.Tooling.LanguageService;

internal sealed record UtilityClassCompletionContext(
    int TokenStart,
    int TokenEnd,
    string TokenText,
    string Prefix)
{
    internal static bool TryCreate(
        string templateText,
        int templateStart,
        int documentOffset,
        out UtilityClassCompletionContext context)
    {
        context = null!;
        if (templateText is null ||
            templateStart < 0 ||
            documentOffset < templateStart ||
            documentOffset > templateStart + templateText.Length)
        {
            return false;
        }

        var token = UtilityCandidateScanner.FindTokenAtPosition(
            templateText,
            documentOffset - templateStart,
            new UtilityCandidateScanOptions
            {
                ContentOffset = templateStart,
            });
        if (token is null)
        {
            return false;
        }

        context = new UtilityClassCompletionContext(
            token.SourceSpan.Start,
            token.SourceSpan.End,
            token.Text,
            token.Prefix);
        return true;
    }
}
