namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// An HTML comment. The C# port of Vue 3.5's <c>CommentNode</c> (<c>@vue/compiler-core</c>
/// <c>ast.ts</c>). <see cref="Content"/> is the text between <c>&lt;!--</c> and <c>--&gt;</c>; the
/// node's <see cref="SyntaxNode.Location"/> covers the delimiters.
/// </summary>
public sealed record CommentNode : TemplateChildNode
{
    /// <summary>The comment body, excluding the delimiters.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Comment;
}
