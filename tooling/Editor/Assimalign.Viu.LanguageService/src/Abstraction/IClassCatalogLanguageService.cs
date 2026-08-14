namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Extends the Viu language service with build-contributed class catalogs for template class-value
/// completion and hover.
/// </summary>
/// <remarks>
/// The host owns project discovery and file-system reads. It feeds immutable JSON snapshots through
/// this contract, while the language service owns format validation, merging, and feature
/// precedence. The contract is build-tool-neutral; any producer can emit the documented class
/// catalog format. [V01.01.12.30]
/// </remarks>
public interface IClassCatalogLanguageService
{
    /// <summary>Configures the class catalogs visible to one document.</summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <param name="configuration">
    /// The immutable catalog snapshot, or <see langword="null"/> to clear every build-contributed
    /// class for the document. Reusing the same instance guarantees that the catalog inputs have not
    /// changed since the previous request.
    /// </param>
    void ConfigureClassCatalogs(
        string documentUri,
        LanguageClassCatalogConfiguration? configuration);
}
