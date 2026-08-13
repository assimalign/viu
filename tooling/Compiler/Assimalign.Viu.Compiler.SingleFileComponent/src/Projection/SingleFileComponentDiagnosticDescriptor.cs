namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One host-neutral entry of the stable VIU diagnostic catalog ([V01.01.06.11]) — the projection
/// library's analogue of a Roslyn <c>DiagnosticDescriptor</c>, carrying exactly the identity both
/// build-time hosts share. The generator's adapter materializes the matching Roslyn descriptor (kept
/// generator-side so RS2008 release tracking stays in the analyzer project); the language service maps
/// to LSP diagnostics directly. The catalog entries are static singletons on
/// <see cref="SingleFileComponentDiagnostics"/>, so a record class preserves both the reference
/// identity and the value equality the incremental pipeline's cache keys rely on.
/// </summary>
/// <param name="Id">The stable VIU-prefixed diagnostic id (<c>VIU1001</c> … <c>VIU1303</c>).</param>
/// <param name="Title">The short descriptor title.</param>
/// <param name="DefaultSeverity">The default severity the diagnostic reports at.</param>
/// <param name="HelpLink">The per-id DIAGNOSTICS.md catalog anchor.</param>
public sealed record SingleFileComponentDiagnosticDescriptor(
    string Id,
    string Title,
    SingleFileComponentDiagnosticSeverity DefaultSeverity,
    string HelpLink);
