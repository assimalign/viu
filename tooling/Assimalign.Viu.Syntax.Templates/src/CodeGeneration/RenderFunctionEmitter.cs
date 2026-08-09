using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Serializes a <see cref="TransformResult"/>'s code-generation tree into a C# render-function body.
/// The selected target either constructs the closed virtual-node algebra against a
/// <c>ComponentRenderFrame</c> or writes server markup directly with subtree-local virtual-node
/// fallbacks. This analyzer-host library does not reference runtime assemblies: runtime type names are
/// written as fully qualified names into consumer code. Specified by <c>[SFC-CG-1]</c>,
/// <c>[SFC-CG-2]</c>, and <c>[SSR-COMPILE-1]</c>.
/// </summary>
/// <remarks>
/// The emission is deterministic — ordinal string handling, invariant-culture numbers, LF newlines — and
/// pure: equal input produces an equal <see cref="RenderFunctionEmitterResult"/>. Virtual-node block
/// assembly remains an explicit open, descendant construction/tracking, and close sequence; the server
/// target preserves that sequence only in fallback regions and emits ordered buffer writes elsewhere.
/// Specified by <c>[RND-BLOCK-3]</c> and <c>[SSR-COMPILE-2]</c>.
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

        string code;
        IReadOnlyList<RenderSourceMapping> mappings;
        switch (options.TargetProfile)
        {
            case RenderFunctionTargetProfile.VirtualNodeTree:
                var frameWriter = new FrameRenderCodeWriter(result, options.IndentLevel, options.IndentText);
                code = frameWriter.EmitRenderBody();
                mappings = frameWriter.SourceMappings;
                break;
            case RenderFunctionTargetProfile.ServerMarkup:
                if (!result.IsServerRendering)
                {
                    throw new InvalidOperationException(
                        "The server-markup target requires a transform with IsServerRendering enabled.");
                }

                var serverWriter = new ServerRenderCodeWriter(result, options.IndentLevel, options.IndentText);
                code = serverWriter.EmitRenderBody();
                mappings = serverWriter.SourceMappings;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.TargetProfile,
                    "Unknown render-function target profile.");
        }

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
