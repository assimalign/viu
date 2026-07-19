namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// The kind of a simple selector, per the W3C Selectors Level 4 simple selectors
/// (https://www.w3.org/TR/selectors-4/#simple). A simple selector is the granularity the scoped rewrite
/// attaches its <c>[data-v-hash]</c> attribute after (the last simple of the last compound).
/// </summary>
public enum CssSimpleSelectorKind
{
    /// <summary>A type (element) selector — <c>div</c>.</summary>
    Type,

    /// <summary>The universal selector — <c>*</c>.</summary>
    Universal,

    /// <summary>A class selector — <c>.foo</c>.</summary>
    Class,

    /// <summary>An id selector — <c>#foo</c>.</summary>
    Id,

    /// <summary>An attribute selector — <c>[type="text"]</c>.</summary>
    Attribute,
}
