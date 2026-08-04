namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The token categories <see cref="ViuLexicalClassifier"/> can emit for a Viu single-file component.
/// </summary>
/// <remarks>
/// <para>
/// A kind is a purely lexical judgement about a span of text. It is deliberately independent of the
/// editor: <see cref="ViuClassificationTypeNames.GetClassificationTypeName(ViuClassificationKind)"/>
/// is the single place a kind becomes a Visual Studio classification type name, so the palette can be
/// re-mapped without touching the lexer and both halves stay unit-testable.
/// </para>
/// <para>
/// The kinds split along one axis: token categories that belong to embedded C# reuse the editor's
/// existing classification types so a script span inherits whatever colors the user chose for C#,
/// while the template, markup, and style categories are Viu-owned and carry the Viu palette.
/// </para>
/// </remarks>
internal enum ViuClassificationKind
{
    /// <summary>A C# keyword, a CSS at-rule, or the legacy <c>@script</c> block header.</summary>
    Keyword,

    /// <summary>A comment in any section.</summary>
    Comment,

    /// <summary>A C# identifier that is neither a type nor a method position.</summary>
    Identifier,

    /// <summary>A plain markup attribute name, or a CSS property name.</summary>
    MarkupAttribute,

    /// <summary>An attribute value, quotes included.</summary>
    MarkupAttributeValue,

    /// <summary>An HTML element tag name — a lowercase tag Viu does not own.</summary>
    MarkupNode,

    /// <summary>A C# method position: call syntax anywhere, or the handler slot of an event binding.</summary>
    Method,

    /// <summary>A numeric literal in C# or CSS, CSS units included.</summary>
    Number,

    /// <summary>A C# operator.</summary>
    Operator,

    /// <summary>C# or CSS punctuation.</summary>
    Punctuation,

    /// <summary>A string literal in C# or CSS.</summary>
    String,

    /// <summary>A C# type name.</summary>
    Type,

    /// <summary>A component tag name — PascalCase or dotted, per the ordinal name resolution of <c>[CMP-6]</c>.</summary>
    Component,

    /// <summary>A directive attribute name: <c>v-*</c>, <c>:bind</c>, <c>@event</c>, or <c>#slot</c>.</summary>
    Directive,

    /// <summary>The <c>{{</c> and <c>}}</c> of an interpolation.</summary>
    InterpolationDelimiter,

    /// <summary>A utility variant prefix inside a class value, such as <c>hover:</c>.</summary>
    UtilityVariant,

    /// <summary>A utility class inside a class value.</summary>
    UtilityClass,

    /// <summary>
    /// A tag name Viu owns: <c>template</c>, <c>slot</c>, <c>style</c>, <c>script</c>, and the legacy
    /// <c>@template</c>/<c>@style</c> block headers.
    /// </summary>
    FrameworkTag,

    /// <summary>Tag punctuation: <c>&lt;</c>, <c>&gt;</c>, <c>&lt;/</c>, <c>/&gt;</c>, and the attribute <c>=</c>.</summary>
    Delimiter,

    /// <summary>A CSS selector.</summary>
    StyleSelector,

    /// <summary>A CSS custom property name (<c>--name</c>) — a theme token.</summary>
    StyleCustomProperty,
}
