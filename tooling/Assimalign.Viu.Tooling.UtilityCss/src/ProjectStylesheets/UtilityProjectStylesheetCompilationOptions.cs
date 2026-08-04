namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Configures one pure project-stylesheet compilation.
/// </summary>
public sealed class UtilityProjectStylesheetCompilationOptions
{
    /// <summary>
    /// Gets or sets the stable identity used for diagnostics and reference-graph lookup.
    /// </summary>
    public string SourceIdentity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the built-in utility registry used by <c>@apply</c>.
    /// </summary>
    public UtilityCssRegistry Registry { get; set; } = UtilityCssRegistry.BuiltIn;

    /// <summary>
    /// Gets or sets the resolved CSS-first theme.
    /// </summary>
    public UtilityTheme Theme { get; set; } = UtilityTheme.Default;

    /// <summary>
    /// Gets or sets the explicit, host-resolved reference graph.
    /// </summary>
    public UtilityStylesheetReferenceGraph ReferenceGraph { get; set; } =
        UtilityStylesheetReferenceGraph.Empty;
}
