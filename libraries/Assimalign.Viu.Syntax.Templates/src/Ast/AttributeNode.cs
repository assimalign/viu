namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// A plain (non-directive) attribute: a literal name and optional literal value, with no expression to
/// evaluate. The transform stage turns it into a static property, so it never contributes a patch flag.
/// </summary>
public sealed record AttributeNode : PropertyNode
{
    /// <summary>The attribute name.</summary>
    public required string Name { get; init; }

    /// <summary>The source range of the attribute name alone.</summary>
    public required SourceLocation NameLocation { get; init; }

    /// <summary>The attribute value text node, or <see langword="null"/> when the attribute has no value.</summary>
    public TextNode? Value { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Attribute;
}
