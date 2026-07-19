namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The output of <see cref="RenderFunctionEmitter.Emit(TransformResult, RenderFunctionEmitterOptions)"/>:
/// the C# render-function body, the cache-slot count the hosting runtime must allocate, and the render
/// source map ([V01.01.05.08]). The C# counterpart of the <c>CodegenResult</c> Vue 3.5's <c>generate()</c>
/// returns (<c>@vue/compiler-core</c> <c>codegen.ts</c>) — <c>code</c> maps to <see cref="Code"/> and
/// <c>map</c> to <see cref="SourceMappings"/>; the <c>preamble</c>/<c>ast</c> members have no counterpart
/// because the composition root owns the method declaration.
/// </summary>
/// <remarks>
/// A record over value-equatable members, so an incremental-generator pipeline stage carrying this result
/// caches correctly: equal transform input produces an equal (and equally hashed) result.
/// </remarks>
public sealed record RenderFunctionEmitterResult
{
    /// <summary>
    /// The emitted render-method body: the component/directive resolution statements followed by the
    /// <c>return</c> of the root block expression. Lines are LF-terminated and indented per
    /// <see cref="RenderFunctionEmitterOptions.IndentLevel"/>; the text ends with a trailing LF.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The number of <c>_cache</c> slots the render function uses (<c>v-once</c> subtrees and cached
    /// handlers). The upstream counterpart is the <c>cached</c> count stamped on the transformed root;
    /// the runtime sizes the per-instance cache array from it because C# arrays, unlike JavaScript
    /// arrays, cannot grow on assignment.
    /// </summary>
    public required int CacheSlotCount { get; init; }

    /// <summary>
    /// The render source map ([V01.01.05.08]): each dynamic template expression's position in
    /// <see cref="Code"/> paired with the template location it came from. The composition root
    /// ([V01.01.06.02]) turns these into <c>#line</c> span directives so a C# compile error inside an
    /// emitted expression resolves to the <c>.viu</c> template rather than to generated code. A
    /// <see cref="SyntaxList{T}"/> so the result stays value-equatable (the caching contract); empty when
    /// the template has no mappable dynamic expressions.
    /// </summary>
    public SyntaxList<RenderSourceMapping> SourceMappings { get; init; } = SyntaxList<RenderSourceMapping>.Empty;
}
