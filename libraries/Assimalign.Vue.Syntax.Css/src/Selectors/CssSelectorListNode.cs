namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A selector list — the comma-separated complex selectors of a qualified rule's prelude (or the inner
/// selector of a functional pseudo-class such as <c>:deep()</c>). The W3C Selectors Level 4
/// <c>&lt;complex-selector-list&gt;</c> (https://www.w3.org/TR/selectors-4/#typedef-complex-selector-list),
/// parsed only as far as the scoped rewrite needs. Its <see cref="SyntaxNode.Location"/> spans the whole
/// prelude.
/// </summary>
public sealed record CssSelectorListNode : CssSyntaxNode
{
    /// <summary>The complex selectors, in source order (the comma-separated groups).</summary>
    public required SyntaxList<CssComplexSelectorNode> Selectors { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.SelectorList;
}
