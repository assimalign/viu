namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies whether a utility compiler diagnostic prevents CSS generation.
/// </summary>
public enum UtilityCssDiagnosticSeverity
{
    /// <summary>The candidate generated CSS but should be updated.</summary>
    Warning,

    /// <summary>The candidate did not generate CSS.</summary>
    Error,
}
