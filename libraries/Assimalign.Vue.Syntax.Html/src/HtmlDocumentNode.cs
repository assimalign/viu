namespace Assimalign.Vue.Syntax.Html;

/// <summary>
/// The document root node — the WHATWG parsing model's document container
/// (https://html.spec.whatwg.org/multipage/parsing.html). In the current scaffold it carries the raw
/// document <see cref="Content"/> spanning the whole source; the child node list replaces raw content
/// when element-level parsing lands.
/// </summary>
public sealed record HtmlDocumentNode : HtmlSyntaxNode
{
    /// <summary>The raw document text — the whole parsed source, pending element-level parsing.</summary>
    public required string Content { get; init; }

    /// <inheritdoc />
    public override HtmlSyntaxNodeKind Kind => HtmlSyntaxNodeKind.Document;
}
