namespace Assimalign.Vue.Syntax.Html;

/// <summary>
/// The base of every plain-HTML syntax node: an immutable, value-comparable record rooting the
/// document tree on the shared <see cref="SyntaxNode"/> contract, with the HTML-specific
/// <see cref="HtmlSyntaxNodeKind"/> discriminator. Node categories follow the WHATWG HTML parsing
/// model (https://html.spec.whatwg.org/multipage/parsing.html).
/// </summary>
/// <remarks>
/// This hierarchy covers plain HTML <em>documents</em> processed by build tooling — above all the WASM
/// host page (<c>wwwroot/index.html</c> entry rewriting, the role Vite's HTML entry processing plays in
/// a Vue build). The Vue template language — HTML-flavored markup plus directives and interpolation —
/// is the separate <c>Assimalign.Vue.Syntax.Templates</c> area; this scaffold never grows template
/// semantics. Scaffold: the tree currently carries only the raw <see cref="HtmlDocumentNode"/>
/// produced by <see cref="HtmlSyntaxParser"/>.
/// </remarks>
public abstract record HtmlSyntaxNode : SyntaxNode
{
    /// <summary>The node kind discriminator.</summary>
    public abstract HtmlSyntaxNodeKind Kind { get; }

    /// <inheritdoc />
    public sealed override int RawKind => (int)Kind;
}
