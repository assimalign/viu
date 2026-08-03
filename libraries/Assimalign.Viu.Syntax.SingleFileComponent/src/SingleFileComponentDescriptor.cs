namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The parsed shape of a <c>.viu</c> single-file component: its blocks and their source spans, from the
/// hybrid container ([V01.01.06.10], decided 2026-08-02; specified by <c>[SFC-3]</c>) — tag-based
/// <c>&lt;template&gt;</c>/<c>&lt;style&gt;</c> blocks plus the @-form <c>@script { }</c> and custom
/// blocks. Immutable and value-equatable: identical file content yields an equal descriptor — the
/// incremental-caching prerequisite of [V01.01.06.02].
/// </summary>
/// <remarks>
/// A file has at most one <see cref="Template"/> and at most one <see cref="Script"/> (a second of
/// either is reported as a duplicate-block diagnostic and ignored, keeping the first), any number of
/// <see cref="Styles"/>, and any number of <see cref="CustomBlocks"/>. Style and custom blocks are
/// deliberately unlimited: several style blocks can carry different options (one scoped, one global,
/// one module) and each contributes, while a custom block's meaning belongs to whatever tooling
/// registered for it, so the container has no basis for a limit. Template and script are singular
/// because both merge into one generated partial class, where a second of either has no coherent
/// meaning. Unlike the <c>.vue</c> compatibility descriptor, this one has a single script slot
/// (<c>[VUE-2]</c>).
/// </remarks>
public sealed record SingleFileComponentDescriptor
{
    /// <summary>The full original <c>.viu</c> source.</summary>
    public required string Source { get; init; }

    /// <summary>The single <c>&lt;template&gt;</c> (or legacy <c>@template</c>) block, or <see langword="null"/> when the file has none.</summary>
    public required SingleFileComponentTemplateBlock? Template { get; init; }

    /// <summary>The single <c>@script</c> block, or <see langword="null"/> when the file has none.</summary>
    public required SingleFileComponentScriptBlock? Script { get; init; }

    /// <summary>The <c>&lt;style&gt;</c> (or legacy <c>@style</c>) blocks, in source order.</summary>
    public required SyntaxList<SingleFileComponentStyleBlock> Styles { get; init; }

    /// <summary>The custom blocks (e.g. <c>@docs</c>), in source order.</summary>
    public required SyntaxList<SingleFileComponentCustomBlock> CustomBlocks { get; init; }
}
