using System;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// Parses a tag-based <c>.vue</c> single-file component into a
/// <see cref="VueSingleFileComponentDescriptor"/> without changing Viu's canonical <c>.viu</c>
/// <c>@</c>-block grammar. This is the [V01.01.06.09] compatibility entry point and follows Vue 3.5's
/// root-container parsing model: <c>&lt;template&gt;</c> contains HTML-like markup while every other
/// root block is raw text until its matching end tag.
/// </summary>
/// <remarks>
/// The parser performs block-level slicing only. It preserves exact source locations and raw content,
/// reports recoverable structural diagnostics through <see cref="VueSingleFileComponentParseResult.Errors"/>,
/// performs no file input/output, reflection, or dynamic code generation, and does not parse the block
/// languages themselves. Container-format references for the input this parser accepts (not statements
/// about Viu's own semantics):
/// <see href="https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts">@vue/compiler-sfc parse.ts</see>
/// and
/// <see href="https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts">@vue/compiler-core tokenizer.ts</see>.
/// </remarks>
public static class VueSingleFileComponentParser
{
    /// <summary>Parses tag-based <c>.vue</c> source into a descriptor and recoverable diagnostics.</summary>
    /// <param name="source">The full <c>.vue</c> file text.</param>
    /// <returns>The parse result, including a descriptor even when the source is malformed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static VueSingleFileComponentParseResult Parse(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new VueSingleFileComponentParseEngine(source).Parse();
    }
}
