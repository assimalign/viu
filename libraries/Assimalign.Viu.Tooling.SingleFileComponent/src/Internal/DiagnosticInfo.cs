using System;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// A value-equatable diagnostic produced by the shared projection, carried inside the generator's
/// cached model. The <see cref="Descriptor"/> is a stable, host-neutral VIU catalog entry
/// ([V01.01.06.11]) and <see cref="Message"/> is the parser's original human-readable text; deferring
/// host materialization (the generator's Roslyn <c>Diagnostic</c>, the language service's LSP
/// diagnostic) keeps the pipeline output comparable so unrelated edits stay cached. The descriptor
/// singletons preserve reference identity and value equality, so cache-key semantics are unchanged
/// from the pre-extraction Roslyn-descriptor form.
/// </summary>
internal readonly record struct DiagnosticInfo(
    SingleFileComponentDiagnosticDescriptor Descriptor,
    LocationInfo Location,
    string Message)
    : IEquatable<DiagnosticInfo>;
