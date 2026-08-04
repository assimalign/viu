namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies whether a recoverable theme diagnostic prevents the affected declaration.
/// </summary>
public enum UtilityThemeDiagnosticSeverity
{
    /// <summary>The input is accepted with a compatibility warning.</summary>
    Warning,

    /// <summary>The affected option, declaration, or block is ignored.</summary>
    Error,
}
