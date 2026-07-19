namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A qualified rule — a selector prelude and a declaration block (e.g. <c>a:hover { color: red }</c>),
/// the CSS Syntax Level 3 <c>&lt;qualified-rule&gt;</c>
/// (https://www.w3.org/TR/css-syntax-3/#qualified-rule). <see cref="Prelude"/> is the exact raw slice of
/// the selector text before the <c>{</c> (trimmed); <see cref="Selectors"/> is that prelude parsed into
/// the selector list the scoped rewrite operates on. <see cref="Declarations"/> holds the block's
/// property/value pairs.
/// </summary>
public sealed record CssQualifiedRuleNode : CssSyntaxNode
{
    /// <summary>The raw selector prelude text, trimmed of surrounding whitespace.</summary>
    public required string Prelude { get; init; }

    /// <summary>The prelude parsed into a selector list — the scoped rewrite's input.</summary>
    public required CssSelectorListNode Selectors { get; init; }

    /// <summary>The declarations in the rule's block, in source order.</summary>
    public required SyntaxList<CssDeclarationNode> Declarations { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.QualifiedRule;
}
