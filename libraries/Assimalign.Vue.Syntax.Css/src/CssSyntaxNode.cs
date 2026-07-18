namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The base of every CSS syntax node: an immutable, value-comparable record rooting the stylesheet
/// tree on the shared <see cref="SyntaxNode"/> contract, with the CSS-specific
/// <see cref="CssSyntaxNodeKind"/> discriminator. Node categories follow the CSS Syntax Module Level 3
/// parser output model (https://www.w3.org/TR/css-syntax-3/#parsing).
/// </summary>
/// <remarks>
/// Scaffold for the CSS language area: the tree currently carries only the raw
/// <see cref="CssStylesheetNode"/> produced by <see cref="CssSyntaxParser"/>. Rule/declaration-level
/// parsing arrives with the scoped-CSS ([V01.01.06.04]) and CSS Modules ([V01.01.06.06]) work, which
/// this hierarchy exists to host — including build-embedded style tooling (e.g. utility-class
/// generation) registered through the aggregate parser seam.
/// </remarks>
public abstract record CssSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract CssSyntaxNodeKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;
}
