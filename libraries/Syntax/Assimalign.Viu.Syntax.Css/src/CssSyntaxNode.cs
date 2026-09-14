namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The base of every CSS syntax node: an immutable, value-comparable record rooting the stylesheet
/// tree on the shared <see cref="SyntaxNode"/> contract, with the CSS-specific
/// <see cref="CssSyntaxNodeKind"/> discriminator. Node categories follow the CSS Syntax Module Level 3
/// parser output model (https://www.w3.org/TR/css-syntax-3/#parsing).
/// </summary>
/// <remarks>The tree retains declarations and parsed selectors for CSS Modules and binding rewrites.</remarks>
public abstract record CssSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract CssSyntaxNodeKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;
}
