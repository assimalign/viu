using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The mutable working form of an <see cref="IfNode"/>: adjacent <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c>
/// siblings are folded into one node whose <see cref="Branches"/> grow as later siblings are processed, and
/// whose <see cref="CodegenNode"/> conditional chain is extended in place. Mirrors the mutable <c>IfNode</c>
/// upstream mutates in <c>@vue/compiler-core</c> <c>transforms/vIf.ts</c>; frozen into an immutable
/// <see cref="IfNode"/> when consumed.
/// </summary>
internal sealed record WorkingIf : TemplateChildNode
{
    /// <summary>The branches, in source order.</summary>
    public List<WorkingIfBranch> Branches { get; } = new();

    /// <summary>The compiled conditional-expression chain (an immutable value, reassigned as branches are appended).</summary>
    public TemplateSyntaxNode? CodegenNode { get; set; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.If;
}
