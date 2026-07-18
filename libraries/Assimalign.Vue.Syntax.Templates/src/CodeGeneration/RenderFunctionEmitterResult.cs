namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The output of <see cref="RenderFunctionEmitter.Emit(TransformResult, RenderFunctionEmitterOptions)"/>:
/// the C# render-function body plus the cache-slot count the hosting runtime must allocate. The C#
/// counterpart of the <c>CodegenResult</c> Vue 3.5's <c>generate()</c> returns
/// (<c>@vue/compiler-core</c> <c>codegen.ts</c>) — <c>code</c> maps to <see cref="Code"/>; the
/// <c>preamble</c>/<c>ast</c>/source-map members have no counterpart because the composition root owns
/// the method declaration and [V01.01.05.08] owns source mapping.
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
}
