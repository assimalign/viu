namespace Assimalign.Viu.Syntax;

/// <summary>
/// The severity of a <see cref="Diagnostic"/>. The member set and ordering mirror Roslyn's
/// <c>Microsoft.CodeAnalysis.DiagnosticSeverity</c> (with the whole-word <see cref="Information"/>
/// for its <c>Info</c>) so parser diagnostics translate one-to-one when a source generator or
/// analyzer re-reports them through the Roslyn diagnostic pipeline ([V01.01.05.05]/[V01.01.06.02]).
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Not surfaced to the user; consumable by tooling.</summary>
    Hidden = 0,

    /// <summary>Informational, not indicating a problem.</summary>
    Information = 1,

    /// <summary>Suspicious but allowed.</summary>
    Warning = 2,

    /// <summary>Not allowed by the language.</summary>
    Error = 3,
}
