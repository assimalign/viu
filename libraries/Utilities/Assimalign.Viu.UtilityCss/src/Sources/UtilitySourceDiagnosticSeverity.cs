namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Identifies the severity of a recoverable source-configuration diagnostic.
/// </summary>
public enum UtilitySourceDiagnosticSeverity
{
    /// <summary>The affected construct is ignored.</summary>
    Error,

    /// <summary>The affected construct remains usable with a documented fallback.</summary>
    Warning,
}
