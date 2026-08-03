namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A custom block such as <c>@docs { }</c> or <c>&lt;docs&gt;&lt;/docs&gt;</c> — any block whose name is
/// not <c>template</c>, <c>script</c>, or <c>style</c>. The parser preserves the block with its options
/// and raw content rather than rejecting it: an unrecognized name is not an error, because the block's
/// meaning belongs to whatever build tooling registered for it, not to the container parser.
/// </summary>
public sealed record SingleFileComponentCustomBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Custom;
}
