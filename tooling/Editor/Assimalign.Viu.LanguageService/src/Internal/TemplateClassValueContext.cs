namespace Assimalign.Viu.LanguageService;

internal sealed record TemplateClassValueContext(
    int TokenStart,
    int TokenEnd,
    string TokenText,
    string Prefix)
{
    internal static bool TryCreate(
        string templateText,
        int templateStart,
        int documentOffset,
        out TemplateClassValueContext context)
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

        context = new TemplateClassValueContext(
            token.TokenStart,
            token.TokenEnd,
            token.TokenText,
            token.Prefix);
        return true;
    }
}
