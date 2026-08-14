namespace Assimalign.Viu.LanguageService;

/// <summary>Resolves a template class token to build-contributed CSS.</summary>
internal static class ClassCatalogHoverProvider
{
    /// <summary>Gets the exact catalog entry hover, or null when no entry matches.</summary>
    internal static LanguageHover? GetHover(
        LanguageDocument document,
        TemplateClassValueContext context,
        ClassCatalogSet catalogs)
    {
        if (context.TokenText.Length == 0 ||
            catalogs.Find(context.TokenText) is not { } entry)
        {
            return null;
        }

        return new LanguageHover(
            LanguageHoverMarkdown.CreateCss(entry.Css),
            new LanguageRange(
                TextCoordinateConverter.GetPosition(document.Text, context.TokenStart),
                TextCoordinateConverter.GetPosition(document.Text, context.TokenEnd)));
    }
}
