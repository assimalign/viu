using System.Linq;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The recoverable result of resolving one candidate across the active registry, theme, and project
/// stylesheet definitions.
/// </summary>
/// <param name="Metadata">Generated compiler and editor metadata when resolution succeeds.</param>
/// <param name="UtilityDiagnostics">Built-in registry diagnostics for the candidate.</param>
/// <param name="DirectiveDiagnostics">
/// Structural diagnostics from the root and referenced project stylesheets.
/// </param>
/// <param name="ProjectDiagnostics">Semantic project-stylesheet diagnostics.</param>
public sealed record UtilityProjectClassResolutionResult(
    UtilityClassMetadata? Metadata,
    UtilityCollection<UtilityCssDiagnostic> UtilityDiagnostics,
    UtilityCollection<UtilityDirectiveDiagnostic> DirectiveDiagnostics,
    UtilityCollection<UtilityProjectStylesheetDiagnostic> ProjectDiagnostics)
{
    /// <summary>
    /// Gets whether metadata was generated without an error diagnostic from any resolution layer.
    /// </summary>
    public bool IsSuccess =>
        Metadata is not null &&
        !UtilityDiagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityCssDiagnosticSeverity.Error) &&
        !DirectiveDiagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityDirectiveDiagnosticSeverity.Error) &&
        !ProjectDiagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityProjectStylesheetDiagnosticSeverity.Error);
}
