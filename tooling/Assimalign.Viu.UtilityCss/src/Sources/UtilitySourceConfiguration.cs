namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The immutable source-detection and virtual-import configuration projected from one project
/// utility stylesheet.
/// </summary>
/// <param name="HasUtilitiesImport">
/// Whether the stylesheet imports the Viu-owned <c>viu-utilities</c> compiler sentinel.
/// </param>
/// <param name="IsAutomaticDetectionEnabled">
/// Whether the SDK should include its default project source items.
/// </param>
/// <param name="BasePath">
/// The optional decoded base path supplied by the import <c>source(...)</c> modifier.
/// </param>
/// <param name="Prefix">The optional lowercase utility prefix supplied by <c>prefix(...)</c>.</param>
/// <param name="IsImportant">
/// Whether every generated utility declaration receives <c>!important</c>.
/// </param>
/// <param name="ImportedThemeOptions">
/// The optional <c>theme(static)</c> or <c>theme(inline)</c> import behavior.
/// </param>
/// <param name="Directives">Valid source directives in source order.</param>
/// <param name="IncludedPaths">Distinct included paths in first-seen order.</param>
/// <param name="ExcludedPaths">Distinct excluded paths in first-seen order.</param>
/// <param name="IncludedCandidates">Distinct included inline candidates in first-seen order.</param>
/// <param name="ExcludedCandidates">Distinct excluded inline candidates in first-seen order.</param>
public sealed record UtilitySourceConfiguration(
    bool HasUtilitiesImport,
    bool IsAutomaticDetectionEnabled,
    string? BasePath,
    string? Prefix,
    bool IsImportant,
    UtilityThemeOptions ImportedThemeOptions,
    UtilityCollection<UtilitySourceDirective> Directives,
    UtilityCollection<string> IncludedPaths,
    UtilityCollection<string> ExcludedPaths,
    UtilityCollection<string> IncludedCandidates,
    UtilityCollection<string> ExcludedCandidates);
