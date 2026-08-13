namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation array literal, e.g. the directive arguments array or the dynamic-slots entries.
/// </summary>
public sealed record ArrayExpression : TemplateSyntaxNode
{
    /// <summary>
    /// The array elements. Each is a literal <see cref="string"/> or a <see cref="TemplateSyntaxNode"/> — a
    /// transform assembles the array from fragments it cannot type uniformly.
    /// </summary>
    public required SyntaxList<object> Elements { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.ArrayExpression;
}
