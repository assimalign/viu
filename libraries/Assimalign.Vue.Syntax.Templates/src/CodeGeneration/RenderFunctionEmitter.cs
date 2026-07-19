using System;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Serializes a <see cref="TransformResult"/>'s code-generation tree into a C# render-function body —
/// the C# port of Vue 3.5's <c>generate()</c> (<c>@vue/compiler-core</c> <c>codegen.ts</c>,
/// https://vuejs.org/guide/extras/rendering-mechanism.html). Every vnode becomes a direct invocation of a
/// runtime helper referenced <b>by name</b> using upstream's aliased spelling (<c>_openBlock</c>,
/// <c>_createElementBlock</c>, <c>_toDisplayString</c>, …, per <see cref="HelperNames"/> and
/// <see cref="TransformContext.HelperString(RuntimeHelper)"/>); this library never references the runtime
/// assembly, and the composition root (the source generator, [V01.01.06.02]) binds the names via a
/// file-level <c>using static</c> of the runtime render-helper surface. The name/signature contract is
/// documented in this library's <c>docs/DESIGN.md</c> and pinned by <c>RenderFunctionEmitterTests</c>.
/// </summary>
/// <remarks>
/// The emission is deterministic — ordinal string handling, invariant-culture numbers, LF newlines — and
/// pure: equal input produces an equal <see cref="RenderFunctionEmitterResult"/>. JavaScript constructs
/// with no C# counterpart are emitted through documented equivalents; the load-bearing one is the block
/// sequence: upstream's comma expression <c>(openBlock(), createElementBlock(...))</c> becomes
/// <c>_createElementBlock(_openBlock(), ...)</c>, relying on C#'s guaranteed left-to-right argument
/// evaluation to open the block before the child arguments are evaluated. See <c>docs/DESIGN.md</c> for
/// the full divergence table.
/// </remarks>
public static class RenderFunctionEmitter
{
    /// <summary>Emits the render body for <paramref name="result"/> with default options.</summary>
    /// <param name="result">The transformed template.</param>
    /// <returns>The emitted render body and cache-slot count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static RenderFunctionEmitterResult Emit(TransformResult result)
        => Emit(result, new RenderFunctionEmitterOptions());

    /// <summary>Emits the render body for <paramref name="result"/>.</summary>
    /// <param name="result">The transformed template.</param>
    /// <param name="options">The emission configuration.</param>
    /// <returns>The emitted render body and cache-slot count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static RenderFunctionEmitterResult Emit(TransformResult result, RenderFunctionEmitterOptions options)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var writer = new RenderCodeWriter(result, options.IndentLevel, options.IndentText);
        return new RenderFunctionEmitterResult
        {
            Code = writer.EmitRenderBody(),
            CacheSlotCount = result.Cached.Count,
        };
    }
}
