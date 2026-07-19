namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation object literal, e.g. a vnode props object or a compiled slots object. The C# port of
/// Vue 3.5's <c>ObjectExpression</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
public sealed record ObjectExpression : TemplateSyntaxNode
{
    /// <summary>The object's properties, in source/emit order.</summary>
    public required SyntaxList<Property> Properties { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsObjectExpression;
}
