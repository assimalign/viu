namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A plain (non-directive) attribute. The C# port of Vue 3.5's <c>AttributeNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>).
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
