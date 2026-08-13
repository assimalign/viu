namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// The degradation-ladder states of project-context resolution ([V01.01.12.23], #259). Every
/// state below <see cref="SemanticsActive"/> keeps the syntax-only features working — resolution
/// failures degrade IntelliSense, they never break the document.
/// </summary>
internal enum ViuProjectContextStatusKind
{
    /// <summary>Restore artifacts resolved and the semantic project context is being served.</summary>
    SemanticsActive,

    /// <summary>The document is a loose file with no owning Viu project — silent, not an error.</summary>
    NoOwningProject,

    /// <summary>The owning project has never produced <c>obj\project.assets.json</c>.</summary>
    AssetsMissing,

    /// <summary>The restore artifacts exist but a required reference could not be resolved.</summary>
    AssetsUnresolvable,

    /// <summary>
    /// The project file is newer than <c>obj\project.assets.json</c>; semantics are still served
    /// from the stale artifact.
    /// </summary>
    AssetsStale,
}
