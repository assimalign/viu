namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation object literal, e.g. a render node's props object or a compiled slots object.
/// </summary>
public sealed record ObjectExpression : TemplateSyntaxNode
{
    /// <summary>The object's properties, in source/emit order.</summary>
    public required SyntaxList<Property> Properties { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsObjectExpression;
}
