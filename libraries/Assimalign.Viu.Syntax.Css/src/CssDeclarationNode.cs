namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// A declaration — a property/value pair inside a rule block (e.g. <c>color: red</c>), the CSS Syntax
/// Level 3 <c>&lt;declaration&gt;</c> (https://www.w3.org/TR/css-syntax-3/#declaration). The
/// <see cref="Value"/> is the exact raw slice between the <c>:</c> and the terminating <c>;</c> or
/// <c>}</c>, trimmed of surrounding whitespace; <see cref="Important"/> records a trailing
/// <c>!important</c>. Value components are not further tokenized — rule-level parsing keeps raw value
/// text, which is all the scoped rewrite (keyframe/animation names) needs.
/// </summary>
public sealed record CssDeclarationNode : CssSyntaxNode
{
    /// <summary>The property name (e.g. <c>color</c>, <c>animation-name</c>).</summary>
    public required string Property { get; init; }

    /// <summary>The declaration value, trimmed, excluding any <c>!important</c> flag.</summary>
    public required string Value { get; init; }

    /// <summary>Whether the declaration carried a trailing <c>!important</c>.</summary>
    public required bool Important { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.Declaration;
}
