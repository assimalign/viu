using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The deterministic result of parsing and projecting CSS-first <c>@theme</c> blocks.
/// </summary>
/// <param name="Theme">The semantic theme overlay used by compiler and editor resolution.</param>
/// <param name="Css">
/// The deterministic custom-property layer contributed by this input. Reference declarations and
/// reset operations do not emit properties.
/// </param>
/// <param name="Declarations">Successfully parsed declarations and resets in source order.</param>
/// <param name="Diagnostics">Recoverable diagnostics in source order.</param>
public sealed record UtilityThemeParseResult(
    UtilityTheme Theme,
    string Css,
    UtilityCollection<UtilityThemeDeclaration> Declarations,
    UtilityCollection<UtilityThemeDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether no error diagnostic was produced.
    /// </summary>
    public bool IsSuccess =>
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityThemeDiagnosticSeverity.Error);
}
