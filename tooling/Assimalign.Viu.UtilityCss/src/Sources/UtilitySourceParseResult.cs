using System.Linq;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The deterministic result of parsing virtual-import and source-detection configuration.
/// </summary>
/// <param name="Configuration">The usable immutable configuration.</param>
/// <param name="Diagnostics">Recoverable diagnostics in source order.</param>
public sealed record UtilitySourceParseResult(
    UtilitySourceConfiguration Configuration,
    UtilityCollection<UtilitySourceDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether no error diagnostic was produced.
    /// </summary>
    public bool IsSuccess =>
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilitySourceDiagnosticSeverity.Error);
}
