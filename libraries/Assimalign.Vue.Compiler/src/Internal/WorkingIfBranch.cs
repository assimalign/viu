using System.Collections.Generic;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The mutable working form of an <see cref="IfBranchNode"/> used while the transform pipeline traverses and
/// rewrites the tree. Structural rewriting (branch grouping, child replacement/removal) needs stable node
/// identity and in-place child lists, which immutable records cannot provide; this working node is frozen
/// into an immutable <see cref="IfBranchNode"/> when its containing element's vnode children are built.
/// Mirrors the mutable <c>IfBranchNode</c> upstream mutates in <c>@vue/compiler-core</c> <c>transforms/vIf.ts</c>.
/// </summary>
internal sealed record WorkingIfBranch : SyntaxNode
{
    /// <summary>The branch condition, or <see langword="null"/> for <c>v-else</c>.</summary>
    public ExpressionNode? Condition { get; set; }

    /// <summary>The mutable working children of the branch (traversed and rewritten in place).</summary>
    public List<SyntaxNode> Children { get; } = new();

    /// <summary>The user-authored <c>key</c> attribute/binding on the branch element, if any.</summary>
    public PropertyNode? UserKey { get; set; }

    /// <summary>Whether the branch came from a <c>&lt;template&gt;</c> container.</summary>
    public bool IsTemplateIf { get; set; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.IfBranch;
}
