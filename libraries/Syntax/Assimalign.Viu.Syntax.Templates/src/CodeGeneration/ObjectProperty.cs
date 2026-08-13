namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A single key/value entry in an <see cref="ObjectExpression"/>. Used for render-node properties, slot
/// objects, and modifier maps.
/// </summary>
public sealed record ObjectProperty : TemplateSyntaxNode
{
    /// <summary>The property key expression (static or dynamic).</summary>
    public required ExpressionNode Key { get; init; }

    /// <summary>The property value — a code-generation node (expression, call, object, function, …).</summary>
    public required TemplateSyntaxNode Value { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.ObjectProperty;
}
