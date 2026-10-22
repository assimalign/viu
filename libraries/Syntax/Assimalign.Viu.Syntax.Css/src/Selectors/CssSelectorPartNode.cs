namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The base of the parts that make up a <see cref="CssComplexSelectorNode"/>: a simple selector
/// (<see cref="CssSimpleSelectorNode"/>), a pseudo selector (<see cref="CssPseudoSelectorNode"/>), or a
/// combinator (<see cref="CssCombinatorNode"/>). Kept flat and source-ordered so the scoped rewrite can
/// find its attribute-insertion point with one reverse scan over the parts, with no tree walk.
/// </summary>
public abstract record CssSelectorPartNode : CssSyntaxNode
{
}
