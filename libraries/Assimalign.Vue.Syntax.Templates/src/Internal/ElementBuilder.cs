using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The mutable working state for an element while its open tag, children, and close tag are being
/// parsed, materialised into an immutable <see cref="ElementNode"/> when the element closes. Vue 3.5's
/// parser mutates the node object in place (pushing children, refining <c>tagType</c>, back-patching
/// <c>loc.end</c>); this port keeps the mutation on a builder so the emitted AST records stay immutable.
/// </summary>
internal sealed class ElementBuilder
{
    /// <summary>Creates a builder for an open tag.</summary>
    /// <param name="tag">The raw tag name.</param>
    /// <param name="elementNamespace">The inferred namespace.</param>
    /// <param name="tagStartOffset">The offset of the opening <c>&lt;</c>.</param>
    public ElementBuilder(string tag, ElementNamespace elementNamespace, int tagStartOffset)
    {
        Tag = tag;
        Namespace = elementNamespace;
        TagStartOffset = tagStartOffset;
        ElementType = ElementType.Element;
    }

    /// <summary>The raw tag name.</summary>
    public string Tag { get; }

    /// <summary>The inferred namespace.</summary>
    public ElementNamespace Namespace { get; }

    /// <summary>The offset of the opening <c>&lt;</c>.</summary>
    public int TagStartOffset { get; }

    /// <summary>The element classification, refined when the close tag is seen.</summary>
    public ElementType ElementType { get; set; }

    /// <summary>Whether the open tag was self-closing.</summary>
    public bool IsSelfClosing { get; set; }

    /// <summary>The finalized properties, in source order.</summary>
    public List<PropertyNode> Properties { get; } = new();

    /// <summary>The finalized child nodes, in source order.</summary>
    public List<TemplateChildNode> Children { get; } = new();
}
