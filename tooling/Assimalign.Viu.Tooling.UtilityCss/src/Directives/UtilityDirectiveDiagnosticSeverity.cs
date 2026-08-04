namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Identifies the impact of a project stylesheet diagnostic.
/// </summary>
public enum UtilityDirectiveDiagnosticSeverity
{
    /// <summary>The affected construct is ignored by later semantic processing.</summary>
    Error,

    /// <summary>The construct remains usable, but the author should review it.</summary>
    Warning,
}
