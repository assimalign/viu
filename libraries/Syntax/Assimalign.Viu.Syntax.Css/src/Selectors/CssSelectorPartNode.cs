namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A source-ordered part of a complex selector: a simple selector, pseudo selector, or combinator.
/// The physical order is retained for CSS Modules traversal and canonical serialization.
/// </summary>
public abstract record CssSelectorPartNode : CssSyntaxNode
{
}
