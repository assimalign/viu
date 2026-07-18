namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The markup namespace an element belongs to, inferred by the parser per the WHATWG tree-construction
/// dispatcher. The C# port of Vue 3.5's <c>Namespaces</c> enum (<c>@vue/compiler-core</c> <c>ast.ts</c>);
/// numeric values match upstream.
/// </summary>
/// <remarks>
/// See the WHATWG namespace switching rules:
/// https://html.spec.whatwg.org/multipage/parsing.html#tree-construction-dispatcher.
/// </remarks>
public enum ElementNamespace
{
    /// <summary>The HTML namespace (upstream <c>HTML</c>).</summary>
    Html = 0,

    /// <summary>The SVG namespace (upstream <c>SVG</c>).</summary>
    Svg = 1,

    /// <summary>The MathML namespace (upstream <c>MATH_ML</c>).</summary>
    MathML = 2,
}
