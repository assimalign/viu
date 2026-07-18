namespace Assimalign.Vue.Syntax;

/// <summary>
/// The uniform diagnostic surface every <c>Assimalign.Vue.Syntax.*</c> parser exposes: a
/// human-readable message, a source location, and an integer code. The code-generation layer that
/// consumes parse output ([V01.01.06.02]) reads diagnostics through this base without knowing which
/// language produced them.
/// </summary>
/// <remarks>
/// This base deliberately unifies <em>only</em> the shape of a diagnostic. Concrete code catalogs and
/// delivery mechanisms stay per-language and MUST NOT be unified here, mirroring upstream: the template
/// compiler pins <c>CompilerErrorCode</c> numerically to <c>@vue/compiler-core</c>'s <c>ErrorCodes</c>
/// and pushes errors through <c>ParserOptions.OnError</c>, while the single-file-component parser owns its
/// Vuecs-defined <c>SingleFileComponentErrorCode</c> catalog and returns errors on its result — the same
/// split as <c>@vue/compiler-core</c> (onError push) versus <c>@vue/compiler-sfc</c> (result errors pull).
/// Each derived record surfaces its own enum-typed code and projects it here as <see cref="RawCode"/>.
/// </remarks>
public abstract record SyntaxDiagnostic
{
    /// <summary>The human-readable message for the diagnostic.</summary>
    public required string Message { get; init; }

    /// <summary>The source range the diagnostic points at.</summary>
    public required SourceLocation Location { get; init; }

    /// <summary>The diagnostic's code as an integer, projected from the derived record's enum-typed code.</summary>
    public abstract int RawCode { get; }
}
