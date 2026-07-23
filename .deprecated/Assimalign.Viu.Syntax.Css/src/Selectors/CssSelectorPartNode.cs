namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The base of the parts that make up a <see cref="CssComplexSelectorNode"/>: a simple selector
/// (<see cref="CssSimpleSelectorNode"/>), a pseudo selector (<see cref="CssPseudoSelectorNode"/>), or a
/// combinator (<see cref="CssCombinatorNode"/>). Kept flat and source-ordered so the scoped rewrite can
/// scan for the attribute-insertion point exactly as Vue's plugin scans the
/// <c>postcss-selector-parser</c> node list.
/// </summary>
public abstract record CssSelectorPartNode : CssSyntaxNode
{
}
