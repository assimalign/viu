using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The deterministic output of executing one CSS-first project stylesheet.
/// </summary>
/// <param name="AuthoredCss">
/// Authored CSS with executable directives rewritten and definition directives removed.
/// </param>
/// <param name="UtilityCss">Generated rules for discovered project-defined utility candidates.</param>
/// <param name="Rules">Generated project-defined utility metadata in deterministic order.</param>
/// <param name="Utilities">Root and referenced custom utility definitions.</param>
/// <param name="Variants">Root and referenced custom variant definitions.</param>
/// <param name="DirectiveDiagnostics">Structural parser diagnostics from every visited stylesheet.</param>
/// <param name="Diagnostics">Semantic execution diagnostics in deterministic source order.</param>
public sealed record UtilityProjectStylesheetCompilationResult(
    string AuthoredCss,
    string UtilityCss,
    UtilityCollection<UtilityClassMetadata> Rules,
    UtilityCollection<UtilityCustomUtilityDefinition> Utilities,
    UtilityCollection<UtilityCustomVariantDefinition> Variants,
    UtilityCollection<UtilityDirectiveDiagnostic> DirectiveDiagnostics,
    UtilityCollection<UtilityProjectStylesheetDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether structural and semantic compilation completed without errors.
    /// </summary>
    public bool IsSuccess =>
        !DirectiveDiagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityDirectiveDiagnosticSeverity.Error) &&
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityProjectStylesheetDiagnosticSeverity.Error);
}
