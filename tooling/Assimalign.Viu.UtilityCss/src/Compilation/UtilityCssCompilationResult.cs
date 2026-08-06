namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// The deterministic output of compiling a candidate set.
/// </summary>
/// <param name="Css">The complete generated utilities layer.</param>
/// <param name="Rules">Resolved rule metadata in emitted order.</param>
/// <param name="Diagnostics">Recoverable diagnostics in candidate discovery order.</param>
public sealed record UtilityCssCompilationResult(
    string Css,
    UtilityCollection<UtilityClassMetadata> Rules,
    UtilityCollection<UtilityCssDiagnostic> Diagnostics);
