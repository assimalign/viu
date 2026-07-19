namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A custom block such as <c>@docs { }</c> — any block whose name is not <c>template</c>, <c>script</c>,
/// or <c>style</c>. Mirrors Vue 3.5's custom blocks (<c>@vue/compiler-sfc</c> <c>customBlocks</c>): the
/// parser preserves the block with its options and raw content rather than rejecting it, so tooling can
/// consume it. See https://vuejs.org/api/sfc-spec.html#custom-blocks.
/// </summary>
public sealed record SingleFileComponentCustomBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Custom;
}
