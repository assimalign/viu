namespace Assimalign.Viu.Syntax;

/// <summary>
/// The uniform diagnostic surface every <c>Assimalign.Viu.Syntax.*</c> parser exposes: a
/// human-readable message, a source location, a severity, and an integer code. The code-generation
/// layer that consumes parse output ([V01.01.06.02]) reads diagnostics through this base without
/// knowing which language produced them.
/// </summary>
/// <remarks>
/// This base deliberately unifies <em>only</em> the shape of a diagnostic. Concrete code catalogs and
/// delivery mechanisms stay per-language and MUST NOT be unified here, because the two differ on both
/// axes: the template compiler owns a frozen <c>CompilerErrorCode</c> numbering and <em>pushes</em>
/// errors through <c>ParserOptions.OnError</c> as it goes, while the single-file-component parser owns
/// its own <c>SingleFileComponentErrorCode</c> catalog and <em>returns</em> errors on its result for the
/// caller to pull. Merging the catalogs would collide the numberings; merging the delivery seams would
/// force one parser to buffer or the other to stream. Each derived record surfaces its own enum-typed
/// code and projects it here as <see cref="RawCode"/>.
/// </remarks>
public abstract record Diagnostic
{
    /// <summary>The human-readable message for the diagnostic.</summary>
    public required string Message { get; init; }

    /// <summary>The source range the diagnostic points at.</summary>
    public required SourceLocation Location { get; init; }

    /// <summary>The diagnostic's severity.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>The diagnostic's code as an integer, projected from the derived record's enum-typed code.</summary>
    public abstract int RawCode { get; }
}
