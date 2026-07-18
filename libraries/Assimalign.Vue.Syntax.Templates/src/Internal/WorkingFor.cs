using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The mutable working form of a <see cref="ForNode"/> used while the transform pipeline traverses the tree.
/// Mirrors the mutable <c>ForNode</c> upstream mutates in <c>@vue/compiler-core</c> <c>transforms/vFor.ts</c>;
/// frozen into an immutable <see cref="ForNode"/> when consumed.
/// </summary>
internal sealed record WorkingFor : TemplateChildNode
{
    /// <summary>The iterated source expression.</summary>
    public required ExpressionNode Source { get; set; }

    /// <summary>The value alias, or <see langword="null"/>.</summary>
    public ExpressionNode? ValueAlias { get; set; }

    /// <summary>The key alias, or <see langword="null"/>.</summary>
    public ExpressionNode? KeyAlias { get; set; }

    /// <summary>The object index alias, or <see langword="null"/>.</summary>
    public ExpressionNode? ObjectIndexAlias { get; set; }

    /// <summary>The decomposed <c>v-for</c> pieces.</summary>
    public required ForParseResult ParseResult { get; set; }

    /// <summary>The mutable working children (the repeated content).</summary>
    public List<TemplateSyntaxNode> Children { get; } = new();

    /// <summary>The compiled fragment-block vnode call (an immutable value, set on exit).</summary>
    public TemplateSyntaxNode? CodegenNode { get; set; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.For;
}
