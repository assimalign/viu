using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The deterministic result of parsing CSS-first utility directives and functions.
/// </summary>
/// <param name="Css">
/// A canonical projection of valid supported directives in source order. It is not an expanded
/// replacement for the surrounding authored stylesheet.
/// </param>
/// <param name="Directives">Supported directive nodes in source order.</param>
/// <param name="Functions">Supported function calls in source order, including nested calls.</param>
/// <param name="Diagnostics">Recoverable diagnostics in source order.</param>
public sealed record UtilityStylesheetParseResult(
    string Css,
    UtilityCollection<UtilityDirective> Directives,
    UtilityCollection<UtilityCssFunction> Functions,
    UtilityCollection<UtilityDirectiveDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether no error diagnostic was produced.
    /// </summary>
    public bool IsSuccess =>
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityDirectiveDiagnosticSeverity.Error);
}
