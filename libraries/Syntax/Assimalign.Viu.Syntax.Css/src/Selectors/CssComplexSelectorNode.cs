namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A complex selector represented as a flat, source-ordered sequence of simple selectors,
/// pseudo selectors, and combinators. The flat representation supports deterministic serialization
/// and CSS Modules class renaming without losing ordering.
/// </summary>
public sealed record CssComplexSelectorNode : CssSyntaxNode
{
    /// <summary>The selector parts, in source order.</summary>
    public required SyntaxList<CssSelectorPartNode> Parts { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.ComplexSelector;
}
