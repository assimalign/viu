namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Discriminates the kinds of node the CSS parser produces, following the CSS Syntax Module Level 3
/// parser output model (https://www.w3.org/TR/css-syntax-3/#parsing): a stylesheet is a list of rules;
/// a rule is a qualified rule or an at-rule; declarations live inside rule blocks. The catalog is
/// Vuecs-defined (there is no upstream Vue numbering to pin) and grows as rule-level parsing lands
/// ([V01.01.06.04]/[V01.01.06.06]).
/// </summary>
public enum CssSyntaxNodeKind
{
    /// <summary>The stylesheet root — the parser's top-level list of rules.</summary>
    Stylesheet = 0,

    /// <summary>A qualified rule — a selector prelude and a declaration block (e.g. <c>a:hover { … }</c>).</summary>
    QualifiedRule = 1,

    /// <summary>An at-rule (e.g. <c>@media</c>, <c>@import</c>, <c>@keyframes</c>).</summary>
    AtRule = 2,

    /// <summary>A declaration — a property/value pair inside a rule block.</summary>
    Declaration = 3,
}
