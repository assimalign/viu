using Assimalign.Viu.Tooling.UtilityCss;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Extends the Viu language service with project-scoped CSS-first utility configuration.
/// </summary>
public interface IUtilityCssLanguageService
{
    /// <summary>
    /// Configures the utility stylesheet used by completion and hover for one document.
    /// </summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <param name="stylesheetText">
    /// The complete CSS-first utility stylesheet, or <see langword="null"/> to use the built-in theme.
    /// </param>
    void ConfigureUtilityStylesheet(string documentUri, string? stylesheetText);

    /// <summary>
    /// Configures utility IntelliSense with explicit stylesheet identity and resolved references.
    /// </summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <param name="stylesheetText">The complete CSS-first utility stylesheet.</param>
    /// <param name="stylesheetIdentity">The stable stylesheet identity used by reference edges.</param>
    /// <param name="referenceGraph">The host-resolved transitive <c>@reference</c> graph.</param>
    void ConfigureUtilityStylesheet(
        string documentUri,
        string? stylesheetText,
        string stylesheetIdentity,
        UtilityStylesheetReferenceGraph referenceGraph);
}
