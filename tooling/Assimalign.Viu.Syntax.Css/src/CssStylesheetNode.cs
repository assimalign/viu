namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The stylesheet root node — the CSS Syntax Level 3 "list of rules" container
/// (https://www.w3.org/TR/css-syntax-3/#parse-stylesheet). Carries the top-level
/// <see cref="Rules"/> (qualified rules and at-rules, in source order); its inherited
/// <see cref="SyntaxNode.Location"/> spans the whole parsed source.
/// </summary>
public sealed record CssStylesheetNode : CssSyntaxNode
{
    /// <summary>The top-level rules — qualified rules and at-rules, in source order.</summary>
    public required SyntaxList<CssSyntaxNode> Rules { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.Stylesheet;
}
