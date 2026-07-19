namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// A keyframe rule inside an <c>@keyframes</c> block — a keyframe selector (a percentage, or <c>from</c>
/// / <c>to</c>, or a comma-separated list of them) and a declaration block, per CSS Animations Level 1
/// (https://www.w3.org/TR/css-animations-1/#keyframes). The keyframe selector is <b>not</b> a CSS
/// selector and is never scoped, so it is kept as the raw <see cref="Selector"/> text rather than parsed
/// into a selector list.
/// </summary>
public sealed record CssKeyframeRuleNode : CssSyntaxNode
{
    /// <summary>The keyframe selector text (e.g. <c>0%</c>, <c>from</c>, <c>50%, 100%</c>), trimmed.</summary>
    public required string Selector { get; init; }

    /// <summary>The declarations in the keyframe's block, in source order.</summary>
    public required SyntaxList<CssDeclarationNode> Declarations { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.KeyframeRule;
}
