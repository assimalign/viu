using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The mutable working state for one attribute or directive as its name, argument, modifiers, and value
/// arrive across several tokenizer callbacks, materialised into an immutable <see cref="AttributeNode"/>
/// or <see cref="DirectiveNode"/> at <see cref="ITokenizerCallbacks.OnAttributeEnd"/>. Mirrors Vue 3.5's
/// mutable <c>currentProp</c> (<c>@vue/compiler-core</c> <c>parser.ts</c>).
/// </summary>
internal sealed class PropertyBuilder
{
    /// <summary>Whether this property is a directive (rather than a plain attribute).</summary>
    public bool IsDirective { get; set; }

    /// <summary>The offset where the property begins (its <c>loc.start</c>).</summary>
    public int StartOffset { get; set; }

    /// <summary>The offset where the property ends (its <c>loc.end</c>), set at attribute end.</summary>
    public int EndOffset { get; set; }

    /// <summary>The attribute name, or the normalized directive name (e.g. <c>bind</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The raw directive name preserving shorthand, argument, and modifiers.</summary>
    public string? RawName { get; set; }

    /// <summary>The attribute name location (also extended while inside a <c>v-pre</c> boundary).</summary>
    public SourceLocation? NameLocation { get; set; }

    /// <summary>The directive argument expression, if any.</summary>
    public ExpressionNode? Argument { get; set; }

    /// <summary>The directive modifier expressions, in source order.</summary>
    public List<SimpleExpressionNode> Modifiers { get; } = new();

    /// <summary>The plain-attribute value text node, if any.</summary>
    public TextNode? Value { get; set; }

    /// <summary>The directive expression (its value), if any.</summary>
    public ExpressionNode? Expression { get; set; }
}
