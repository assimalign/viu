namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// The value-equatable read of one single-file-component source: its format, path, full text,
/// collision state, and resolved C# names — everything
/// <see cref="SingleFileComponentProjection.Project"/> needs, and nothing host-specific. In the
/// generator this is the first pipeline stage's output — a pure data record with no Roslyn
/// <c>AdditionalText</c>, <c>SourceText</c>, or compilation reference — so downstream parse/codegen
/// stages re-run only when the file's text or resolved names actually change, keeping the incremental
/// pipeline and IDE responsiveness intact.
/// </summary>
/// <param name="Format">The source's canonical <c>.viu</c> or compatibility <c>.vue</c> container format.</param>
/// <param name="FilePath">The absolute source file path (the diagnostic and hint-name anchor).</param>
/// <param name="FileName">The leaf file name, used verbatim in the generated scaffold header.</param>
/// <param name="Text">The full single-file-component source text.</param>
/// <param name="Namespace">The resolved containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="ClassName">The resolved generated partial class name.</param>
/// <param name="HintName">The resolved, unique <c>AddSource</c> hint name.</param>
/// <param name="ScopeId">The resolved scoped-CSS scope id (<c>data-v-&lt;hash&gt;</c>), derived from the project-relative path ([V01.01.06.04]).</param>
/// <param name="HotReloadComponentIdentifier">
/// The development-only path-stable component identifier, or <see langword="null"/> when hot-reload
/// metadata is disabled.
/// </param>
/// <param name="HasCanonicalPeer">
/// Whether this is a <c>.vue</c> compatibility file whose same-directory, same-base <c>.viu</c> source
/// takes precedence. Such a file reports VIU1004 and does not generate a second partial class. A
/// multi-file host concern carried alongside the input for the generator's pipeline filters;
/// <see cref="SingleFileComponentProjection.Project"/> never reads it.
/// </param>
public readonly record struct SingleFileComponentProjectionInput(
    SingleFileComponentFormat Format,
    string FilePath,
    string FileName,
    string Text,
    string? Namespace,
    string ClassName,
    string HintName,
    string ScopeId,
    string? HotReloadComponentIdentifier,
    bool HasCanonicalPeer)
{
    /// <summary>
    /// Whether the consuming project explicitly targets server rendering and therefore requires a
    /// second, direct-markup render body in addition to the ordinary virtual-node render body.
    /// The value is project-scoped so individual component files cannot select divergent profiles.
    /// Specified by <c>[SSR-TARGET-1]</c> and <c>[SSR-TARGET-2]</c>.
    /// </summary>
    public bool EmitServerRendering { get; init; }
}
