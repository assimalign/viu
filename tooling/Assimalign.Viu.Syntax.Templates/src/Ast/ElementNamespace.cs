namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The markup namespace an element belongs to, inferred by the parser per the WHATWG tree-construction
/// dispatcher.
/// </summary>
/// <remarks>
/// See the WHATWG namespace switching rules:
/// https://html.spec.whatwg.org/multipage/parsing.html#tree-construction-dispatcher.
/// </remarks>
public enum ElementNamespace
{
    /// <summary>The HTML namespace.</summary>
    Html = 0,

    /// <summary>The SVG namespace.</summary>
    Svg = 1,

    /// <summary>The MathML namespace.</summary>
    MathML = 2,
}
