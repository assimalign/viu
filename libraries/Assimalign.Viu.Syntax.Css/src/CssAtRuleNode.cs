namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// An at-rule — a <c>@</c>-keyword, a prelude, and either a <c>{ … }</c> block or a terminating
/// <c>;</c>, the CSS Syntax Level 3 <c>&lt;at-rule&gt;</c> (https://www.w3.org/TR/css-syntax-3/#at-rule).
/// Conditional-group rules (<c>@media</c>, <c>@supports</c>, <c>@container</c>) recurse: their
/// <see cref="Body"/> holds nested <see cref="CssQualifiedRuleNode"/>/<see cref="CssAtRuleNode"/> the
/// rewrite descends into. An <c>@keyframes</c> body holds <see cref="CssKeyframeRuleNode"/> children
/// (its name is scoped and referencing animation values rewritten). Declaration-only at-rules
/// (<c>@font-face</c>, <c>@page</c>) hold <see cref="CssDeclarationNode"/> children; statement at-rules
/// (<c>@import</c>, <c>@charset</c>) have <see cref="HasBlock"/> <see langword="false"/> and an empty body.
/// </summary>
public sealed record CssAtRuleNode : CssSyntaxNode
{
    /// <summary>The at-keyword name without its leading <c>@</c> (e.g. <c>media</c>, <c>keyframes</c>), exactly as authored.</summary>
    public required string Name { get; init; }

    /// <summary>The raw prelude text between the name and the block or <c>;</c>, trimmed of surrounding whitespace.</summary>
    public required string Prelude { get; init; }

    /// <summary>Whether the at-rule had a <c>{ … }</c> block (versus a <c>;</c>-terminated statement).</summary>
    public required bool HasBlock { get; init; }

    /// <summary>
    /// The block's children in source order — nested rules for conditional-group at-rules, keyframe
    /// rules for <c>@keyframes</c>, declarations for declaration-only at-rules; empty for statement
    /// at-rules.
    /// </summary>
    public required SyntaxList<CssSyntaxNode> Body { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.AtRule;
}
