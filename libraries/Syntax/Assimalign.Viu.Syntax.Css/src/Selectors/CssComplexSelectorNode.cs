namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A complex selector — a flat, source-order sequence of <see cref="CssSelectorPartNode"/> parts
/// (simple selectors, pseudo selectors, and the combinators between compound selectors), the W3C
/// Selectors Level 4 <c>&lt;complex-selector&gt;</c>
/// (https://www.w3.org/TR/selectors-4/#typedef-complex-selector). The model is deliberately flat rather
/// than a nested compound tree, because the one question the scoped rewrite asks is positional — which
/// compound receives the <c>[data-v-hash]</c> attribute, namely the last part that is neither a
/// combinator nor a pseudo — and a flat list answers it with a single reverse scan.
/// </summary>
public sealed record CssComplexSelectorNode : CssSyntaxNode
{
    /// <summary>The selector parts, in source order.</summary>
    public required SyntaxList<CssSelectorPartNode> Parts { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.ComplexSelector;
}
