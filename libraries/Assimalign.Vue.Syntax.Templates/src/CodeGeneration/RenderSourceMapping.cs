namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// One span in the render source map ([V01.01.05.08]): a dynamic template expression's position in the
/// emitted render body paired with the template-relative <see cref="SourceLocation"/> it came from. The
/// composition root (the source generator, [V01.01.06.02]) composes <see cref="TemplateLocation"/> with the
/// <c>@template</c> block's content-start position and emits a C# <c>#line</c> span directive so a C#
/// compile error inside the emitted expression (an unresolved member under permissive binding metadata, for
/// example) resolves to the offending <c>.viu</c> template line and column rather than to opaque generated
/// code — the render-body analogue of the <c>@script</c> merge's <c>#line</c> map.
/// </summary>
/// <remarks>
/// The C# counterpart of a segment in the <c>SourceMapGenerator</c> output Vue 3.5's <c>generate()</c>
/// produces (<c>@vue/compiler-core</c> <c>codegen.ts</c>, gated on <c>sourceMap</c>): upstream records
/// original/generated line+column pairs into a standard JavaScript source map; Vuecs records the same
/// correspondence for the Roslyn <c>#line</c> mechanism instead, because the diagnostics travel through the
/// C# compiler, not a browser devtools source map.
/// <para>
/// A record so the render result stays value-equatable — equal template input yields an equal map, the
/// incremental-generator caching contract. Generated positions are zero-based and relative to the emitted
/// render body (the <see cref="RenderFunctionEmitterResult.Code"/> string); <see cref="TemplateLocation"/>
/// positions are in the template block's own coordinate space, exactly as the AST carries them.
/// </para>
/// </remarks>
public sealed record RenderSourceMapping
{
    /// <summary>The zero-based line, within the emitted render body, the mapped expression sits on.</summary>
    public required int GeneratedLine { get; init; }

    /// <summary>
    /// The zero-based column, within its generated line, where the mapped expression's original template
    /// text begins — the <c>#line</c> span directive's character offset. This points at the original
    /// identifier inside the rewritten emission (past any inserted <c>_ctx.</c> prefix), so the mapped span
    /// aligns one-to-one with the template source it came from.
    /// </summary>
    public required int GeneratedColumn { get; init; }

    /// <summary>The template-block-relative source location the mapped expression came from.</summary>
    public required SourceLocation TemplateLocation { get; init; }
}
