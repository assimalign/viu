namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The kind of combinator between two compound selectors, per the W3C Selectors Level 4 combinators
/// (https://www.w3.org/TR/selectors-4/#combinators).
/// </summary>
public enum CssCombinatorKind
{
    /// <summary>The descendant combinator — whitespace between compounds (<c>a b</c>).</summary>
    Descendant,

    /// <summary>The child combinator — <c>&gt;</c> (<c>a &gt; b</c>).</summary>
    Child,

    /// <summary>The next-sibling combinator — <c>+</c> (<c>a + b</c>).</summary>
    NextSibling,

    /// <summary>The subsequent-sibling combinator — <c>~</c> (<c>a ~ b</c>).</summary>
    SubsequentSibling,
}
