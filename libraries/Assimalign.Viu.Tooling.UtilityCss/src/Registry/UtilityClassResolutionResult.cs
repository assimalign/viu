using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// The recoverable result of resolving one parsed class candidate through the utility registry.
/// </summary>
/// <param name="Metadata">Compiler and editor metadata when resolution succeeds.</param>
/// <param name="Diagnostics">Warnings and errors in stable discovery order.</param>
public sealed record UtilityClassResolutionResult(
    UtilityClassMetadata? Metadata,
    UtilityCollection<UtilityCssDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether metadata was produced and no error diagnostic is present.
    /// </summary>
    public bool IsSuccess =>
        Metadata is not null &&
        !Diagnostics.Any(
            diagnostic => diagnostic.Severity == UtilityCssDiagnosticSeverity.Error);
}
