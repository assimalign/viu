namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The base of every CSS syntax node: an immutable, value-comparable record rooting the stylesheet
/// tree on the shared <see cref="SyntaxNode"/> contract, with the CSS-specific
/// <see cref="CssSyntaxNodeKind"/> discriminator. Node categories follow the CSS Syntax Module Level 3
/// parser output model (https://www.w3.org/TR/css-syntax-3/#parsing).
/// </summary>
/// <remarks>
/// Rule-level parsing landed with the scoped-CSS work ([V01.01.06.04]): the tree carries the
/// <see cref="CssStylesheetNode"/> root, its qualified rules and at-rules, their declarations, and — for
/// qualified rules — the parsed selector list the scoped rewrite reads. CSS Modules ([V01.01.06.06])
/// extends the hierarchy further, as does build-embedded style tooling (e.g. utility-class generation)
/// registered through the aggregate parser seam.
/// </remarks>
public abstract record CssSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract CssSyntaxNodeKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;
}
