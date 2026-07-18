namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The stylesheet root node — the CSS Syntax Level 3 "list of rules" container
/// (https://www.w3.org/TR/css-syntax-3/#parse-stylesheet). In the current scaffold it carries the raw
/// stylesheet <see cref="Content"/> spanning the whole source; the child rule list replaces raw
/// content when rule-level parsing lands ([V01.01.06.04]/[V01.01.06.06]).
/// </summary>
public sealed record CssStylesheetNode : CssSyntaxNode
{
    /// <summary>The raw stylesheet text — the whole parsed source, pending rule-level parsing.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override CssSyntaxNodeKind Kind => CssSyntaxNodeKind.Stylesheet;
}
