namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A complex selector — a flat, source-order sequence of <see cref="CssSelectorPartNode"/> parts
/// (simple selectors, pseudo selectors, and the combinators between compound selectors), the W3C
/// Selectors Level 4 <c>&lt;complex-selector&gt;</c>
/// (https://www.w3.org/TR/selectors-4/#typedef-complex-selector). The flat model mirrors
/// <c>postcss-selector-parser</c>'s node list, which is the representation Vue's scoped plugin walks to
/// pick the compound that receives the <c>[data-v-hash]</c> attribute: the last part that is neither a
/// combinator nor a pseudo (see <c>@vue/compiler-sfc</c> <c>pluginScoped.ts</c>).
/// </summary>
public sealed record CssComplexSelectorNode : CssSyntaxNode
{
    /// <summary>The selector parts, in source order.</summary>
    public required SyntaxList<CssSelectorPartNode> Parts { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.ComplexSelector;
}
