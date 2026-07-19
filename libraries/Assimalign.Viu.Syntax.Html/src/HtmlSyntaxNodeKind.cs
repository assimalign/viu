namespace Assimalign.Viu.Syntax.Html;

/// <summary>
/// Discriminates the kinds of node the HTML parser produces, following the WHATWG HTML parsing model
/// (https://html.spec.whatwg.org/multipage/parsing.html). The catalog is Viu-defined (there is no
/// upstream Vue numbering to pin) and grows as element-level parsing lands.
/// </summary>
public enum HtmlSyntaxNodeKind
{
    /// <summary>The document root.</summary>
    Document = 0,

    /// <summary>The <c>&lt;!DOCTYPE …&gt;</c> preamble.</summary>
    Doctype = 1,

    /// <summary>An element and its attributes.</summary>
    Element = 2,

    /// <summary>A run of character data.</summary>
    Text = 3,

    /// <summary>An HTML comment.</summary>
    Comment = 4,
}
