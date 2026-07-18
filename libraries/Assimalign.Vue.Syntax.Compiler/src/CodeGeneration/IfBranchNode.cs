namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A single branch of a grouped <see cref="IfNode"/> chain — one <c>v-if</c>, <c>v-else-if</c>, or
/// <c>v-else</c>. The C# port of Vue 3.5's <c>IfBranchNode</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
public sealed record IfBranchNode : TemplateSyntaxNode
{
    /// <summary>The branch condition, or <see langword="null"/> for the <c>v-else</c> branch.</summary>
    public ExpressionNode? Condition { get; init; }

    /// <summary>
    /// The branch's rendered children. For a <c>&lt;template v-if&gt;</c> without <c>v-for</c> these are the
    /// template's own children; otherwise the single directive-bearing element.
    /// </summary>
    public required SyntaxList<TemplateChildNode> Children { get; init; }

    /// <summary>
    /// The user-authored <c>key</c> attribute or binding on the branch element, if any (used to diagnose
    /// duplicate keys across branches). A <see cref="AttributeNode"/> or <see cref="DirectiveNode"/>.
    /// </summary>
    public PropertyNode? UserKey { get; init; }

    /// <summary>Whether this branch came from a <c>&lt;template&gt;</c> container carrying the directive.</summary>
    public bool IsTemplateIf { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.IfBranch;
}
