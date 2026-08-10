using System;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The single mapping from a lexical <see cref="ViuClassificationKind"/> to the name of the Visual
/// Studio classification type that colors it, plus the fallback chain used when a name is not
/// registered in the running instance.
/// </summary>
/// <remarks>
/// <para>
/// This type deliberately holds no editor references. It is compiled into the extension assembly and
/// <c>&lt;Compile Include&gt;</c>-linked into the test project, which is how the palette table is
/// pinned by tests even though the classic VSSDK extension itself cannot be referenced from a
/// <c>dotnet test</c> project.
/// </para>
/// <para>
/// The mapping splits in two. <b>Embedded C#</b> — every kind a C# token pass can emit — resolves the
/// classification types the editor and Roslyn already register, so <c>@script</c> blocks,
/// interpolation interiors, and binding-expression interiors inherit the colors the user chose for
/// C#. The literal names are spelled out here rather than taken from
/// <c>PredefinedClassificationTypeNames</c> or Roslyn's <c>ClassificationTypeNames</c> so this file
/// stays editor-free; they are the same strings those constants carry.
/// </para>
/// <para>
/// <b>Template, markup, and style</b> constructs resolve Viu-owned classification types, registered by
/// this extension with default colors and user-editable entries under
/// Tools &gt; Options &gt; Fonts and Colors. The <c>viu.</c> prefix keeps them from colliding with any
/// other extension's names, and their display names are prefixed "Viu — " so they group together in
/// that list.
/// </para>
/// </remarks>
internal static class ViuClassificationTypeNames
{
    /// <summary>Framework tag names: <c>template</c>, <c>slot</c>, <c>style</c>, <c>script</c>.</summary>
    public const string Tag = "viu.tag";

    /// <summary>HTML element tag names.</summary>
    public const string Element = "viu.element";

    /// <summary>Component tag names.</summary>
    public const string Component = "viu.component";

    /// <summary>Directive attribute names.</summary>
    public const string Directive = "viu.directive";

    /// <summary>Plain attribute names and CSS property names.</summary>
    public const string Attribute = "viu.attribute";

    /// <summary>Attribute values, quotes included, and every part of a class value.</summary>
    public const string AttributeValue = "viu.attribute.value";

    /// <summary>The <c>{{</c> and <c>}}</c> of an interpolation.</summary>
    public const string InterpolationDelimiter = "viu.interpolation.delimiter";

    /// <summary>Tag punctuation.</summary>
    public const string Delimiter = "viu.delimiter";

    /// <summary>CSS selectors.</summary>
    public const string StyleSelector = "viu.style.selector";

    /// <summary>CSS custom property names.</summary>
    public const string StyleCustomProperty = "viu.style.custom.property";

    /// <summary>The editor's keyword classification.</summary>
    public const string Keyword = "keyword";

    /// <summary>The editor's string-literal classification.</summary>
    public const string String = "string";

    /// <summary>The editor's numeric-literal classification.</summary>
    public const string Number = "number";

    /// <summary>The editor's comment classification.</summary>
    public const string Comment = "comment";

    /// <summary>The editor's operator classification.</summary>
    public const string Operator = "operator";

    /// <summary>Roslyn's punctuation classification.</summary>
    public const string Punctuation = "punctuation";

    /// <summary>The editor's identifier classification.</summary>
    public const string Identifier = "identifier";

    /// <summary>Roslyn's type-name classification.</summary>
    public const string ClassName = "class name";

    /// <summary>Roslyn's method-name classification.</summary>
    public const string MethodName = "method name";

    /// <summary>Roslyn's namespace-name classification.</summary>
    public const string NamespaceName = "namespace name";

    /// <summary>Roslyn's delegate-name classification.</summary>
    public const string DelegateName = "delegate name";

    /// <summary>Roslyn's enum-name classification.</summary>
    public const string EnumName = "enum name";

    /// <summary>Roslyn's interface-name classification.</summary>
    public const string InterfaceName = "interface name";

    /// <summary>Roslyn's struct-name classification.</summary>
    public const string StructName = "struct name";

    /// <summary>Roslyn's type-parameter-name classification.</summary>
    public const string TypeParameterName = "type parameter name";

    /// <summary>Roslyn's property-name classification.</summary>
    public const string PropertyName = "property name";

    /// <summary>Roslyn's field-name classification.</summary>
    public const string FieldName = "field name";

    /// <summary>Roslyn's constant-name classification.</summary>
    public const string ConstantName = "constant name";

    /// <summary>Roslyn's enum-member-name classification.</summary>
    public const string EnumMemberName = "enum member name";

    /// <summary>Roslyn's event-name classification.</summary>
    public const string EventName = "event name";

    /// <summary>Roslyn's parameter-name classification.</summary>
    public const string ParameterName = "parameter name";

    /// <summary>Roslyn's local-name classification.</summary>
    public const string LocalName = "local name";

    /// <summary>Roslyn's label-name classification.</summary>
    public const string LabelName = "label name";

    /// <summary>
    /// Returns the name of the classification type that colors <paramref name="classificationKind"/>.
    /// </summary>
    /// <param name="classificationKind">The lexical kind to map.</param>
    /// <returns>A registered classification type name; never <see langword="null"/> or empty.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ViuClassificationKind.UtilityVariant"/> and
    /// <see cref="ViuClassificationKind.UtilityClass"/> both map to
    /// <see cref="AttributeValue"/>: a class attribute is deliberately one uninterrupted color, so a
    /// list of utilities reads as a value rather than as a syntax exhibit. The lexer keeps the two
    /// kinds distinct because the language server and the Visual Studio Code grammar still act on the
    /// distinction.
    /// </para>
    /// <para>
    /// Every unmapped kind falls back to <see cref="Identifier"/>, which the core editor always
    /// registers, so a kind added without a palette entry stays visible instead of vanishing.
    /// </para>
    /// </remarks>
    public static string GetClassificationTypeName(ViuClassificationKind classificationKind) =>
        classificationKind switch
        {
            // Embedded C#: the user's own theme owns these colors.
            ViuClassificationKind.Keyword => Keyword,
            ViuClassificationKind.Comment => Comment,
            ViuClassificationKind.Identifier => Identifier,
            ViuClassificationKind.Method => MethodName,
            ViuClassificationKind.Number => Number,
            ViuClassificationKind.Operator => Operator,
            ViuClassificationKind.Punctuation => Punctuation,
            ViuClassificationKind.String => String,
            ViuClassificationKind.Type => ClassName,

            // Template, markup, and style: the Viu palette.
            ViuClassificationKind.FrameworkTag => Tag,
            ViuClassificationKind.MarkupNode => Element,
            ViuClassificationKind.Component => Component,
            ViuClassificationKind.Directive => Directive,
            ViuClassificationKind.MarkupAttribute => Attribute,
            ViuClassificationKind.MarkupAttributeValue => AttributeValue,
            ViuClassificationKind.UtilityVariant => AttributeValue,
            ViuClassificationKind.UtilityClass => AttributeValue,
            ViuClassificationKind.InterpolationDelimiter => InterpolationDelimiter,
            ViuClassificationKind.Delimiter => Delimiter,
            ViuClassificationKind.StyleSelector => StyleSelector,
            ViuClassificationKind.StyleCustomProperty => StyleCustomProperty,
            _ => Identifier,
        };

    /// <summary>
    /// Returns the classification type to try when <paramref name="classificationTypeName"/> is not
    /// registered in the running Visual Studio instance, or <see langword="null"/> when there is
    /// nothing further to try.
    /// </summary>
    /// <param name="classificationTypeName">The name that failed to resolve.</param>
    /// <returns>The next name to try, or <see langword="null"/>.</returns>
    /// <remarks>
    /// In process these names are all present once the managed-language workload is installed:
    /// <see cref="Punctuation"/> and the semantic name constants are contributed by Roslyn's editor
    /// features rather than by the core editor. Resolving defensively keeps the
    /// VSIX independent of any particular workload — a Visual Studio installation without C# still
    /// colors Viu templates, and script spans degrade to plain identifiers instead of losing their
    /// spans entirely. The Viu-owned names need no fallback: this extension registers them itself.
    /// </remarks>
    public static string? GetFallbackClassificationTypeName(string classificationTypeName)
    {
        if (string.Equals(classificationTypeName, MethodName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, ClassName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, NamespaceName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, DelegateName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, EnumName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, InterfaceName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, StructName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, TypeParameterName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, PropertyName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, FieldName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, ConstantName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, EnumMemberName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, EventName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, ParameterName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, LocalName, StringComparison.Ordinal) ||
            string.Equals(classificationTypeName, LabelName, StringComparison.Ordinal))
        {
            return Identifier;
        }

        if (string.Equals(classificationTypeName, Punctuation, StringComparison.Ordinal))
        {
            return Operator;
        }

        return null;
    }
}
