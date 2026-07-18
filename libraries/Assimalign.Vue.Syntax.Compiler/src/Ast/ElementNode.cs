namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// An element with its resolved namespace, classification, properties, and children. The C# port of
/// Vue 3.5's <c>ElementNode</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>). <see cref="ElementType"/> is
/// refined from <see cref="Compiler.ElementType.Element"/> to component/slot/template when the close
/// tag is seen.
/// </summary>
public sealed record ElementNode : TemplateChildNode
{
    /// <summary>The raw tag name exactly as authored.</summary>
    public required string Tag { get; init; }

    /// <summary>The namespace inferred for this element (HTML/SVG/MathML).</summary>
    public required ElementNamespace Namespace { get; init; }

    /// <summary>Whether this element is a native element, component, slot outlet, or template container.</summary>
    public required ElementType ElementType { get; init; }

    /// <summary>The attributes and directives on the open tag, in source order.</summary>
    public required SyntaxList<PropertyNode> Properties { get; init; }

    /// <summary>The child nodes, after whitespace management.</summary>
    public required SyntaxList<TemplateChildNode> Children { get; init; }

    /// <summary>Whether the open tag used self-closing syntax (<c>&lt;br/&gt;</c>).</summary>
    public bool IsSelfClosing { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Element;
}
