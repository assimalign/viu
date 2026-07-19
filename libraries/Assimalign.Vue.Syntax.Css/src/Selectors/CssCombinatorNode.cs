namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A combinator between two compound selectors (descendant, child, next-sibling, or subsequent-sibling).
/// A combinator is never the attribute-insertion point of the scoped rewrite; the whitespace it may span
/// is normalized on serialization.
/// </summary>
public sealed record CssCombinatorNode : CssSelectorPartNode
{
    /// <summary>The combinator kind.</summary>
    public required CssCombinatorKind Combinator { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.Combinator;
}
