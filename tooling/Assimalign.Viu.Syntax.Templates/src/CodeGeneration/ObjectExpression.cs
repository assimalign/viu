namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A code-generation object literal, e.g. a render node's properties object or a compiled slots object.
/// </summary>
public sealed record ObjectExpression : TemplateSyntaxNode
{
    /// <summary>The object's properties, in source/emit order.</summary>
    public required SyntaxList<ObjectProperty> Properties { get; init; }

    /// <summary>
    /// Whether this literal is a directive-modifier bag (<c>name → true</c>) rather than a property
    /// bag. The two emit through different runtime helpers because a directive binding types its
    /// modifiers <c>IReadOnlyDictionary&lt;string, bool&gt;</c> while a property bag is
    /// <c>IReadOnlyDictionary&lt;string, object?&gt;</c>; emitting the property helper into the
    /// modifier slot loses every modifier at run time. Specified by <c>[SFC-CG-6]</c>.
    /// </summary>
    public bool IsDirectiveModifiers { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.ObjectExpression;
}
