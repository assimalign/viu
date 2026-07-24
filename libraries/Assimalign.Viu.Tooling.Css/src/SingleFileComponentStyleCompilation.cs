using System.Collections.Generic;

using Assimalign.Viu.Syntax.Css;

namespace Assimalign.Viu.Tooling.Css;

/// <summary>
/// The result of compiling one <c>.viu</c> file's <c>@style</c> blocks
/// (<see cref="SingleFileComponentStyleCompiler.Compile"/>) — the shared, deterministic output both
/// build-time hosts consume ([V01.01.12.12]). The <c>Assimalign.Viu.Generators.Syntax</c> generator maps it
/// into its cached model to emit the <c>ScopeId</c>/<c>ExtractedStyles</c> constants, the <c>$style</c>
/// accessors, and the <c>v-bind()</c> seam; the <c>ViuBundleCss</c> MSBuild task consumes only
/// <see cref="ExtractedStyles"/> to write the physical bundle. Because both hosts call this one method over
/// the same input, the generated constant and the physical file are byte-identical by construction.
/// </summary>
/// <param name="ScopeId">
/// The scoped-CSS scope id (<c>data-v-&lt;hash&gt;</c>) when the component declares at least one
/// <c>scoped</c> <c>@style</c> block, otherwise <see langword="null"/> ([V01.01.06.04]).
/// </param>
/// <param name="ExtractedStyles">
/// The component's compiled CSS — <c>scoped</c> blocks rewritten with <see cref="ScopeId"/>, <c>module</c>
/// class names locally hashed, <c>v-bind()</c> rewritten to custom properties, and untouched non-scoped
/// blocks passed through verbatim, concatenated in source order — or <see langword="null"/> when the
/// component declares no <c>@style</c> block. This is the exact text the generator emits as its
/// <c>ExtractedStyles</c> constant and the task writes into the bundle.
/// </param>
/// <param name="ModuleClasses">
/// The CSS Modules class map ([V01.01.06.06]) — one entry per <c>original → hashed</c> class, grouped by its
/// accessor. Empty when no <c>@style module</c> block is declared.
/// </param>
/// <param name="VariableBindings">
/// The <c>v-bind()</c> CSS bindings ([V01.01.06.06]) — one entry per distinct <c>(hash, expression)</c>, in
/// first-seen source order, each paired with its block-content-start position so a host can compose
/// per-binding diagnostics onto <c>.viu</c> coordinates. Empty when no <c>@style</c> block uses <c>v-bind()</c>.
/// </param>
/// <param name="Diagnostics">
/// Recoverable diagnostics from the <c>@style</c> rewrites (malformed <c>v-bind()</c>), each paired with its
/// block-content-start position so the host can locate it on the <c>.viu</c> file. Empty when every block
/// compiled cleanly.
/// </param>
public sealed record SingleFileComponentStyleCompilation(
    string? ScopeId,
    string? ExtractedStyles,
    IReadOnlyList<SingleFileComponentStyleModuleClass> ModuleClasses,
    IReadOnlyList<SingleFileComponentStyleVariableBinding> VariableBindings,
    IReadOnlyList<SingleFileComponentStyleDiagnostic> Diagnostics)
{
    /// <summary>An empty compilation — no <c>@style</c> block was declared.</summary>
    public static readonly SingleFileComponentStyleCompilation Empty = new(
        ScopeId: null,
        ExtractedStyles: null,
        ModuleClasses: System.Array.Empty<SingleFileComponentStyleModuleClass>(),
        VariableBindings: System.Array.Empty<SingleFileComponentStyleVariableBinding>(),
        Diagnostics: System.Array.Empty<SingleFileComponentStyleDiagnostic>());
}
