namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A run of static text. <see cref="Content"/> is the decoded text (character references resolved); the
/// node's <see cref="SyntaxNode.Location"/> <c>Source</c> is the raw, undecoded slice.
/// </summary>
public sealed record TextNode : TemplateChildNode
{
    /// <summary>The decoded text content.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Text;
}
