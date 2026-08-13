namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Extends the Viu language service with per-document project-context configuration for semantic
/// script features ([V01.01.12.23], #259).
/// </summary>
/// <remarks>
/// The host owns every file-system probe, resolves the project's restore artifacts into an editor-neutral
/// <see cref="LanguageProjectContext"/>, and feeds it here; the service caches the context per
/// document and serves features from the cache. A host that resolves nothing (a loose file, a
/// project that has never been restored) passes <see langword="null"/> and the service falls back
/// to its syntax-only behavior.
/// </remarks>
public interface IScriptSemanticLanguageService
{
    /// <summary>
    /// Configures the resolved project context used by semantic script features for one document.
    /// </summary>
    /// <param name="documentUri">The editor document URI.</param>
    /// <param name="context">
    /// The materialized project context, or <see langword="null"/> to clear the document's context
    /// and fall back to syntax-only features.
    /// </param>
    void ConfigureProjectContext(string documentUri, LanguageProjectContext? context);
}
