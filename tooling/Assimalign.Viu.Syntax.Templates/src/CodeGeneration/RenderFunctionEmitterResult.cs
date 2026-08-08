namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The output of <see cref="RenderFunctionEmitter.Emit(TransformResult, RenderFunctionEmitterOptions)"/>:
/// the C# render-function body, the cache-slot count the hosting runtime must allocate, and the render
/// source map ([V01.01.05.08]). There is no preamble member: the composition root owns the method
/// declaration and supplies the component instance and render frame (<c>[SFC-CG-1]</c>,
/// <c>[SFC-CG-2]</c>).
/// </summary>
/// <remarks>
/// A record over value-equatable members, so an incremental-generator pipeline stage carrying this result
/// caches correctly: equal transform input produces an equal (and equally hashed) result.
/// </remarks>
public sealed record RenderFunctionEmitterResult
{
    /// <summary>
    /// The emitted render-method body: ordered frame operations, direct virtual-node construction, and
    /// the <c>return</c> of the root node. Lines are LF-terminated and indented per
    /// <see cref="RenderFunctionEmitterOptions.IndentLevel"/>; the text ends with a trailing LF.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The number of frame-owned cache slots the render function uses for cached subtrees, properties,
    /// handlers, and memo metadata. The composition root emits this as the component contract's exact
    /// render-cache size (<c>[SFC-CG-1]</c>). The count must be exact because the per-mount frame allocates
    /// its cache once at that size.
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
