using System;
using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins the Viu color theme's mapping table: which classification type colors each lexical kind, and
/// what happens when a name the running Visual Studio instance does not register is asked for.
/// </summary>
/// <remarks>
/// The table is keyed by enum member name rather than by the enum itself because
/// <see cref="ViuClassificationKind"/> is internal to the extension while xUnit requires public test
/// signatures. Naming the members also makes the theory's failure output readable.
/// </remarks>
public class ViuClassificationTypeNamesTests
{
    /// <summary>
    /// The whole palette, in one place. Kinds that can come out of a C# token pass resolve the
    /// classification types the editor and Roslyn already register, so a script span inherits the
    /// user's own C# colors; template, markup, and style kinds resolve the Viu-owned types this
    /// extension registers.
    /// </summary>
    private static readonly string[][] PaletteEntries =
    [
        [nameof(ViuClassificationKind.Keyword), "keyword"],
        [nameof(ViuClassificationKind.String), "string"],
        [nameof(ViuClassificationKind.Number), "number"],
        [nameof(ViuClassificationKind.Comment), "comment"],
        [nameof(ViuClassificationKind.Operator), "operator"],
        [nameof(ViuClassificationKind.Punctuation), "punctuation"],
        [nameof(ViuClassificationKind.Identifier), "identifier"],
        [nameof(ViuClassificationKind.Type), "class name"],
        [nameof(ViuClassificationKind.Method), "method name"],
        [nameof(ViuClassificationKind.FrameworkTag), "viu.tag"],
        [nameof(ViuClassificationKind.MarkupNode), "viu.element"],
        [nameof(ViuClassificationKind.Component), "viu.component"],
        [nameof(ViuClassificationKind.Directive), "viu.directive"],
        [nameof(ViuClassificationKind.MarkupAttribute), "viu.attribute"],
        [nameof(ViuClassificationKind.MarkupAttributeValue), "viu.attribute.value"],
        [nameof(ViuClassificationKind.UtilityVariant), "viu.attribute.value"],
        [nameof(ViuClassificationKind.UtilityClass), "viu.attribute.value"],
        [nameof(ViuClassificationKind.InterpolationDelimiter), "viu.interpolation.delimiter"],
        [nameof(ViuClassificationKind.Delimiter), "viu.delimiter"],
        [nameof(ViuClassificationKind.StyleSelector), "viu.style.selector"],
        [nameof(ViuClassificationKind.StyleCustomProperty), "viu.style.custom.property"],
    ];

    public static TheoryData<string, string> PaletteMapping
    {
        get
        {
            TheoryData<string, string> paletteMapping = [];
            foreach (string[] paletteEntry in PaletteEntries)
            {
                paletteMapping.Add(paletteEntry[0], paletteEntry[1]);
            }

            return paletteMapping;
        }
    }

    [Theory]
    [MemberData(nameof(PaletteMapping))]
    public void GetClassificationTypeName_EveryKind_MapsToItsPaletteEntry(
        string classificationKindName,
        string expectedClassificationTypeName)
    {
        ViuClassificationKind classificationKind = (ViuClassificationKind)Enum.Parse(
            typeof(ViuClassificationKind),
            classificationKindName);

        ViuClassificationTypeNames.GetClassificationTypeName(classificationKind)
            .ShouldBe(expectedClassificationTypeName);
    }

    [Fact]
    public void GetClassificationTypeName_EveryDeclaredKind_IsCoveredByThePaletteTable()
    {
        // The table above is the palette. A kind added without an entry there would silently take the
        // catch-all, so the enum and the table are pinned to each other.
        HashSet<string> mappedKindNames = [];
        foreach (string[] paletteEntry in PaletteEntries)
        {
            mappedKindNames.Add(paletteEntry[0]);
        }

        string[] declaredKindNames = Enum.GetNames(typeof(ViuClassificationKind));

        declaredKindNames.ShouldAllBe(kindName => mappedKindNames.Contains(kindName));
        mappedKindNames.Count.ShouldBe(declaredKindNames.Length);
    }

    [Fact]
    public void GetClassificationTypeName_UtilityKinds_ShareTheAttributeValueEntry()
    {
        // A class attribute is deliberately one uninterrupted color: utility classes and their
        // variant prefixes are not split apart in the editor, even though the lexer keeps the
        // distinction for the language server and the Visual Studio Code grammar.
        ViuClassificationTypeNames.GetClassificationTypeName(ViuClassificationKind.UtilityClass)
            .ShouldBe(ViuClassificationTypeNames.AttributeValue);
        ViuClassificationTypeNames.GetClassificationTypeName(ViuClassificationKind.UtilityVariant)
            .ShouldBe(ViuClassificationTypeNames.AttributeValue);
        ViuClassificationTypeNames.GetClassificationTypeName(
                ViuClassificationKind.MarkupAttributeValue)
            .ShouldBe(ViuClassificationTypeNames.AttributeValue);
    }

    [Theory]
    [InlineData("method name", "identifier")]
    [InlineData("class name", "identifier")]
    [InlineData("namespace name", "identifier")]
    [InlineData("delegate name", "identifier")]
    [InlineData("enum name", "identifier")]
    [InlineData("interface name", "identifier")]
    [InlineData("struct name", "identifier")]
    [InlineData("type parameter name", "identifier")]
    [InlineData("property name", "identifier")]
    [InlineData("field name", "identifier")]
    [InlineData("constant name", "identifier")]
    [InlineData("enum member name", "identifier")]
    [InlineData("event name", "identifier")]
    [InlineData("parameter name", "identifier")]
    [InlineData("local name", "identifier")]
    [InlineData("label name", "identifier")]
    [InlineData("punctuation", "operator")]
    public void GetFallbackClassificationTypeName_RoslynContributedNames_DegradeToCoreEditorNames(
        string classificationTypeName,
        string expectedFallback)
    {
        // These are contributed by Roslyn's editor features rather than the core editor, so an
        // installation without a managed-language workload must still color Viu documents.
        ViuClassificationTypeNames.GetFallbackClassificationTypeName(classificationTypeName)
            .ShouldBe(expectedFallback);
    }

    [Theory]
    [InlineData("identifier")]
    [InlineData("keyword")]
    [InlineData("viu.tag")]
    [InlineData("viu.attribute.value")]
    public void GetFallbackClassificationTypeName_TerminalNames_HaveNoFurtherFallback(
        string classificationTypeName)
    {
        // The core-editor names are always present, and this extension registers every viu.* name
        // itself, so the chain terminates rather than looping.
        ViuClassificationTypeNames.GetFallbackClassificationTypeName(classificationTypeName)
            .ShouldBeNull();
    }
}
