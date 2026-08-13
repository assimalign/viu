namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One <c>.viu</c> file's projection result ([V01.01.06.11]): the scaffold model to emit (always
/// present — the descriptor is produced even for malformed input) and the mapped diagnostics to report.
/// Both are value-equatable so the generator's incremental pipeline stays cacheable, and both are
/// host-neutral so the language service consumes the identical projection the build produces.
/// </summary>
/// <param name="Model">The scaffold model to emit.</param>
/// <param name="Diagnostics">The mapped neutral diagnostics to report.</param>
public readonly record struct SingleFileComponentProjectionResult(
    SingleFileComponentModel Model,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>
    /// The component usages this template writes ([SFC-USE-1]). The projection cannot validate them —
    /// which components exist is a compilation-wide question — so it publishes the manifest and the host
    /// joins it against the resolved declaration catalog. Empty when the template uses no component.
    /// </summary>
    public EquatableArray<ComponentUsage> ComponentUsages { get; init; }
}
