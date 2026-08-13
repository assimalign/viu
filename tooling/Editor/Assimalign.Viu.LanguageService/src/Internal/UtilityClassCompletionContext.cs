namespace Assimalign.Viu.LanguageService;

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

        var token = TemplateClassTokenScanner.FindTokenAtPosition(
            templateText,
            templateStart,
            documentOffset);
        if (token is null)
        {
            return false;
        }

        context = new UtilityClassCompletionContext(
            token.TokenStart,
            token.TokenEnd,
            token.TokenText,
            token.Prefix);
        return true;
    }
}
