using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Serializes a <see cref="TransformResult"/>'s code-generation tree into a C# render-function body.
/// Every render node becomes ordered statements against a caller-provided component instance and
/// <c>ComponentRenderFrame</c>, with direct construction of the closed virtual-node algebra. This
/// analyzer-host library still does not reference runtime assemblies: adopted runtime types are written
/// as fully qualified names into consumer code. Specified by <c>[SFC-CG-1]</c> and <c>[SFC-CG-2]</c>.
/// </summary>
/// <remarks>
/// The emission is deterministic — ordinal string handling, invariant-culture numbers, LF newlines — and
/// pure: equal input produces an equal <see cref="RenderFunctionEmitterResult"/>. Block assembly is an
/// explicit sequence: <c>frame.OpenBlock()</c>, descendant construction and tracking, then
/// <c>frame.CloseBlock()</c> in the owning node's render plan. This makes lifetime and nesting visible
/// without ambient state. Specified by <c>[RND-BLOCK-3]</c>.
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

        var writer = new FrameRenderCodeWriter(result, options.IndentLevel, options.IndentText);
        var code = writer.EmitRenderBody();
        var mappings = writer.SourceMappings;
        return new RenderFunctionEmitterResult
        {
            Code = code,
            CacheSlotCount = result.Cached.Count,
            // The render source map ([V01.01.05.08]) the composition root turns into #line span directives.
            SourceMappings = mappings.Count == 0
                ? SyntaxList<RenderSourceMapping>.Empty
                : new SyntaxList<RenderSourceMapping>(ToArray(mappings)),
        };
    }

    private static RenderSourceMapping[] ToArray(IReadOnlyList<RenderSourceMapping> mappings)
    {
        var array = new RenderSourceMapping[mappings.Count];
        for (var index = 0; index < mappings.Count; index++)
        {
            array[index] = mappings[index];
        }

        return array;
    }
}
