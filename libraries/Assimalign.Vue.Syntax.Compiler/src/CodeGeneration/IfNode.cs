namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// A grouped <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c> chain: the adjacent conditional siblings are folded
/// into one node with ordered <see cref="Branches"/>. The C# port of Vue 3.5's <c>IfNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>). See https://vuejs.org/guide/essentials/conditional.html.
/// </summary>
/// <remarks>
/// Each branch compiles to its own block with a stable synthetic <c>key</c> so switching branches replaces
/// rather than patches across branches. <see cref="CodegenNode"/> is the compiled
/// <see cref="ConditionalExpression"/> chain (or a <see cref="CacheExpression"/> when combined with
/// <c>v-once</c>).
/// </remarks>
public sealed record IfNode : TemplateChildNode
{
    /// <summary>The branches, in source order: the <c>v-if</c> first, then any <c>v-else-if</c>/<c>v-else</c>.</summary>
    public required SyntaxList<IfBranchNode> Branches { get; init; }

    /// <summary>The compiled conditional-expression chain, or <see langword="null"/> before code generation.</summary>
    public TemplateSyntaxNode? CodegenNode { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.If;
}
