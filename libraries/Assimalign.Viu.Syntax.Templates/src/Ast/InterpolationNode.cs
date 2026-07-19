namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A mustache interpolation such as <c>{{ value }}</c>. The C# port of Vue 3.5's
/// <c>InterpolationNode</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>). The node's
/// <see cref="SyntaxNode.Location"/> covers the delimiters; <see cref="Content"/> covers the inner
/// expression with surrounding whitespace trimmed.
/// </summary>
public sealed record InterpolationNode : TemplateChildNode
{
    /// <summary>The interpolated expression.</summary>
    public required ExpressionNode Content { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Interpolation;
}
