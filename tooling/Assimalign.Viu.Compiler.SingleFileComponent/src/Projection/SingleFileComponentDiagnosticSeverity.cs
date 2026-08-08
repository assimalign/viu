namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The host-neutral default severity of a <see cref="SingleFileComponentDiagnosticDescriptor"/>
/// ([V01.01.06.11]). The projection library never names a Roslyn <c>DiagnosticSeverity</c> in its
/// output: the generator's adapter maps to Roslyn severities, and the language service maps to
/// LSP severities, each at its own edge.
/// </summary>
public enum SingleFileComponentDiagnosticSeverity
{
    /// <summary>An informational message (Roslyn <c>Info</c>, LSP <c>Information</c>).</summary>
    Information,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error.</summary>
    Error,
}
