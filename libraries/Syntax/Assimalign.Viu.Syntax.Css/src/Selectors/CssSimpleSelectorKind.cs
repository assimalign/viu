namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Classifies simple selector syntax for CSS Modules processing and canonical serialization.
/// The categories follow W3C Selectors Level 4 (https://www.w3.org/TR/selectors-4/#simple).
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
