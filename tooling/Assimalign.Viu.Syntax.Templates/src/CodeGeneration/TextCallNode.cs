namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A text child pre-converted to a <c>createTextVNode(...)</c> call so the runtime skips normalization.
/// Produced by the text transform when an element has mixed or multiple text/interpolation children —
/// exactly the case where the runtime would otherwise have to inspect each child's type.
/// </summary>
public sealed record TextCallNode : TemplateChildNode
{
    /// <summary>The wrapped text content: a <see cref="TextNode"/>, <see cref="InterpolationNode"/>, or <see cref="CompoundExpressionNode"/>.</summary>
    public required TemplateSyntaxNode Content { get; init; }

    /// <summary>The compiled <c>createTextVNode</c> call.</summary>
    public required TemplateSyntaxNode CodegenNode { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.TextCall;
}
