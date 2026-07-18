namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// A run of static text. The C# port of Vue 3.5's <c>TextNode</c> (<c>@vue/compiler-core</c>
/// <c>ast.ts</c>). <see cref="Content"/> is the decoded text (character references resolved); the
/// node's <see cref="SyntaxNode.Location"/> <c>Source</c> is the raw, undecoded slice.
/// </summary>
public sealed record TextNode : TemplateChildNode
{
    /// <summary>The decoded text content.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Text;
}
