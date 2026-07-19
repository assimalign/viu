namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The parsed shape of a <c>.viu</c> single-file component: its blocks and their source spans. The C#
/// port of Vue 3.5's <c>SFCDescriptor</c> (<c>@vue/compiler-sfc</c> <c>parse.ts</c>), adapted to the
/// <c>.viu</c> @-block container (a documented Viu divergence from Vue's tag wrappers, decided
/// 2026-07-17). Immutable and value-equatable: identical file content yields an equal descriptor — the
/// incremental-caching prerequisite of [V01.01.06.02].
/// </summary>
/// <remarks>
/// A file has at most one <see cref="Template"/> and at most one <see cref="Script"/> (a second of
/// either is reported as a duplicate-block diagnostic and ignored, keeping the first), any number of
/// <see cref="Styles"/>, and any number of <see cref="CustomBlocks"/> — mirroring Vue's tolerance for
/// multiple <c>&lt;style&gt;</c> and custom blocks. The Vue <c>&lt;script setup&gt;</c> distinction is
/// script analysis and is deferred to [V01.01.06.03]. See https://vuejs.org/api/sfc-spec.html.
/// </remarks>
public sealed record SingleFileComponentDescriptor
{
    /// <summary>The full original <c>.viu</c> source.</summary>
    public required string Source { get; init; }

    /// <summary>The single <c>@template</c> block, or <see langword="null"/> when the file has none.</summary>
    public required SingleFileComponentTemplateBlock? Template { get; init; }

    /// <summary>The single <c>@script</c> block, or <see langword="null"/> when the file has none.</summary>
    public required SingleFileComponentScriptBlock? Script { get; init; }

    /// <summary>The <c>@style</c> blocks, in source order.</summary>
    public required SyntaxList<SingleFileComponentStyleBlock> Styles { get; init; }

    /// <summary>The custom blocks (e.g. <c>@docs</c>), in source order.</summary>
    public required SyntaxList<SingleFileComponentCustomBlock> CustomBlocks { get; init; }
}
