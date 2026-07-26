namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The parsed shape of a tag-based <c>.vue</c> single-file component. Mirrors Vue 3.5's
/// <c>SFCDescriptor</c> block slots while reusing Viu's immutable block and source-location types.
/// See https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts.
/// </summary>
/// <remarks>
/// This descriptor is deliberately separate from <see cref="SingleFileComponentDescriptor"/>. The
/// canonical <c>.viu</c> grammar has one script slot, while Vue permits one ordinary
/// <c>&lt;script&gt;</c> and one <c>&lt;script setup&gt;</c> in the same component. Keeping separate
/// slots preserves that valid source without changing established <c>.viu</c> semantics.
/// </remarks>
public sealed record VueSingleFileComponentDescriptor
{
    /// <summary>The full original <c>.vue</c> source.</summary>
    public required string Source { get; init; }

    /// <summary>The single <c>&lt;template&gt;</c> block, or <see langword="null"/> when absent.</summary>
    public required SingleFileComponentTemplateBlock? Template { get; init; }

    /// <summary>The ordinary <c>&lt;script&gt;</c> block, or <see langword="null"/> when absent.</summary>
    public required SingleFileComponentScriptBlock? Script { get; init; }

    /// <summary>The <c>&lt;script setup&gt;</c> block, or <see langword="null"/> when absent.</summary>
    public required SingleFileComponentScriptBlock? ScriptSetup { get; init; }

    /// <summary>The <c>&lt;style&gt;</c> blocks, in source order.</summary>
    public required SyntaxList<SingleFileComponentStyleBlock> Styles { get; init; }

    /// <summary>The custom top-level blocks, in source order.</summary>
    public required SyntaxList<SingleFileComponentCustomBlock> CustomBlocks { get; init; }
}
