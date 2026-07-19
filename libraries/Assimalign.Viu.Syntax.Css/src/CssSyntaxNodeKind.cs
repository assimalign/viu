namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Discriminates the kinds of node the CSS parser produces, following the CSS Syntax Module Level 3
/// parser output model (https://www.w3.org/TR/css-syntax-3/#parsing): a stylesheet is a list of rules;
/// a rule is a qualified rule or an at-rule; declarations live inside rule blocks. The prelude of a
/// qualified rule is parsed into a selector list (sufficient for scoped-CSS rewriting per the W3C
/// Selectors grammar, https://www.w3.org/TR/selectors-4/), so the catalog also covers the selector tree.
/// The catalog is Viu-defined (there is no upstream Vue numbering to pin) and grew from the scaffold
/// with the scoped-CSS work ([V01.01.06.04]); CSS Modules ([V01.01.06.06]) extends it further.
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

    /// <summary>A keyframe rule inside an <c>@keyframes</c> block — a keyframe selector and a declaration block.</summary>
    KeyframeRule = 4,

    /// <summary>A selector list — the comma-separated complex selectors of a qualified rule's prelude.</summary>
    SelectorList = 5,

    /// <summary>A complex selector — a sequence of compound selectors joined by combinators.</summary>
    ComplexSelector = 6,

    /// <summary>A combinator between compound selectors (descendant, child, next-sibling, subsequent-sibling).</summary>
    Combinator = 7,

    /// <summary>A simple selector — a type, universal, class, id, or attribute selector.</summary>
    SimpleSelector = 8,

    /// <summary>A pseudo-class or pseudo-element selector (including <c>:deep()</c>, <c>:slotted()</c>, <c>:global()</c>).</summary>
    PseudoSelector = 9,
}
