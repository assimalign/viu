using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins Viu's template-owned class-token and attribute-value cursor model.
/// </summary>
public class TemplateClassTokenScannerTests
{
    private const string CaretMarker = "[|]";
    private const int TemplateStart = 120;

    [Theory]
    [InlineData("class")]
    [InlineData("CLASS")]
    public void FindTokenAtPosition_StaticClassValue_ReturnsAbsoluteTokenAndCaretPrefix(
        string attributeName)
    {
        var markedTemplate = $"<div {attributeName}=\"flex ga{CaretMarker}p-4\"></div>";
        var token = FindToken(markedTemplate, out var templateText);
        var expectedTokenStart =
            TemplateStart + templateText.IndexOf("gap-4", StringComparison.Ordinal);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(expectedTokenStart);
        token.TokenEnd.ShouldBe(expectedTokenStart + "gap-4".Length);
        token.TokenText.ShouldBe("gap-4");
        token.Prefix.ShouldBe("ga");
    }

    [Theory]
    [InlineData(
        ":class",
        "condition ? 'hover:bg-[|]red-500' : fallback",
        "hover:bg-red-500",
        "hover:bg-")]
    [InlineData(
        "v-bind:class",
        "{ 'focus:ring-[|]2': active }",
        "focus:ring-2",
        "focus:ring-")]
    public void FindTokenAtPosition_BoundClassStringLiteral_ReturnsLiteralToken(
        string attributeName,
        string markedValue,
        string expectedText,
        string expectedPrefix)
    {
        var markedTemplate = $"<div {attributeName}=\"{markedValue}\"></div>";
        var token = FindToken(markedTemplate, out var templateText);
        var expectedTokenStart =
            TemplateStart + templateText.IndexOf(expectedText, StringComparison.Ordinal);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(expectedTokenStart);
        token.TokenEnd.ShouldBe(expectedTokenStart + expectedText.Length);
        token.TokenText.ShouldBe(expectedText);
        token.Prefix.ShouldBe(expectedPrefix);
    }

    [Theory]
    [InlineData(":class")]
    [InlineData("v-bind:class")]
    public void FindTokenAtPosition_BoundClassObjectKey_ReturnsUnquotedKey(
        string attributeName)
    {
        var markedTemplate =
            $"<div {attributeName}=\"{{ hidden[|]: isHidden, active: isActive }}\"></div>";

        var token = FindToken(markedTemplate, out var templateText);
        var expectedTokenStart =
            TemplateStart + templateText.IndexOf("hidden", StringComparison.Ordinal);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(expectedTokenStart);
        token.TokenEnd.ShouldBe(expectedTokenStart + "hidden".Length);
        token.TokenText.ShouldBe("hidden");
        token.Prefix.ShouldBe("hidden");
    }

    [Fact]
    public void FindTokenAtPosition_WhitespaceBetweenStaticClasses_ReturnsEmptyToken()
    {
        const string markedTemplate = "<div class=\"flex [|] hidden\"></div>";

        var token = FindToken(markedTemplate, out _);
        var documentPosition = GetDocumentPosition(markedTemplate);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(documentPosition);
        token.TokenEnd.ShouldBe(documentPosition);
        token.TokenText.ShouldBeEmpty();
        token.Prefix.ShouldBeEmpty();
    }

    [Fact]
    public void FindTokenAtPosition_UnquotedStaticClassValue_ReturnsToken()
    {
        const string markedTemplate = "<div class=gap-[|]4></div>";

        var token = FindToken(markedTemplate, out var templateText);
        var expectedTokenStart =
            TemplateStart + templateText.IndexOf("gap-4", StringComparison.Ordinal);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(expectedTokenStart);
        token.TokenEnd.ShouldBe(expectedTokenStart + "gap-4".Length);
        token.TokenText.ShouldBe("gap-4");
        token.Prefix.ShouldBe("gap-");
    }

    [Fact]
    public void FindTokenAtPosition_BalancedSelectorWhitespace_PreservesWholeToken()
    {
        const string candidate = "[&:has(> img[alt='hero image'])]:block";
        const string markedTemplate =
            "<div class=\"[&:has(> img[alt='hero [|]image'])]:block flex\"></div>";

        var token = FindToken(markedTemplate, out var templateText);
        var expectedTokenStart =
            TemplateStart + templateText.IndexOf(candidate, StringComparison.Ordinal);

        token.ShouldNotBeNull();
        token.TokenStart.ShouldBe(expectedTokenStart);
        token.TokenEnd.ShouldBe(expectedTokenStart + candidate.Length);
        token.TokenText.ShouldBe(candidate);
        token.Prefix.ShouldBe("[&:has(> img[alt='hero ");
    }

    [Fact]
    public void FindTokenAtPosition_EscapedAttributeQuote_DoesNotEndClassValue()
    {
        const string markedTemplate =
            """<div class="content-[\"hero > image\"] ga[|]p-4"></div>""";

        var token = FindToken(markedTemplate, out _);

        token.ShouldNotBeNull();
        token.TokenText.ShouldBe("gap-4");
        token.Prefix.ShouldBe("ga");
    }

    [Theory]
    [InlineData("<!-- <div class=\"gap-[|]4\"></div> -->")]
    [InlineData("<!-- <div class=\"gap-[|]4\"></div>")]
    public void FindTokenAtPosition_ClassInsideHtmlComment_ReturnsNull(
        string markedTemplate)
        => FindToken(markedTemplate, out _).ShouldBeNull();

    [Fact]
    public void FindTokenAtPosition_ClassOnClosingTag_ReturnsNull()
        => FindToken("</div class=\"gap-[|]4\">", out _).ShouldBeNull();

    [Theory]
    [InlineData("<div class=\"bg-${co[|]lor}-500\"></div>")]
    [InlineData("<div class=\"bg-{{co[|]lor}}\"></div>")]
    [InlineData("<div class=\"bg-{co[|]lor}-500\"></div>")]
    [InlineData("<div :class=\"'bg-[|]' + color\"></div>")]
    [InlineData("<div :class=\"color + 'bg-[|]'\"></div>")]
    [InlineData("<div :class=\"Shell[|]Class\"></div>")]
    [InlineData("<div :class=\"{ hidden: Shell[|]Class }\"></div>")]
    public void FindTokenAtPosition_DynamicOrInterpolatedFragment_ReturnsNull(
        string markedTemplate)
        => FindToken(markedTemplate, out _).ShouldBeNull();

    [Fact]
    public void FindTokenAtPosition_StaticSiblingOfDynamicFragment_ReturnsLiteralToken()
    {
        const string markedTemplate =
            "<div class=\"bg-${color}-500 fl[|]ex\"></div>";

        var token = FindToken(markedTemplate, out _);

        token.ShouldNotBeNull();
        token.TokenText.ShouldBe("flex");
        token.Prefix.ShouldBe("fl");
    }

    [Theory]
    [InlineData("<div title=\"ordinary [|]value\"></div>")]
    [InlineData("<button @click=\"Handle[|]Click\"></button>")]
    [InlineData("<div v-if=\"Is[|]Visible\"></div>")]
    [InlineData("<div class=\"gap-[|]4\"></div>")]
    [InlineData("<div :class=\"Shell[|]Class\"></div>")]
    public void IsInsideAttributeValue_QuotedValue_ReturnsTrue(
        string markedTemplate)
        => IsInsideAttributeValue(markedTemplate).ShouldBeTrue();

    [Fact]
    public void IsInsideAttributeValue_ClosingQuoteBoundary_ReturnsTrue()
        => IsInsideAttributeValue("<div title=\"value[|]\"></div>").ShouldBeTrue();

    [Fact]
    public void IsInsideAttributeValue_OpeningQuoteBoundary_ReturnsFalse()
        => IsInsideAttributeValue("<div title=[|]\"value\"></div>").ShouldBeFalse();

    [Fact]
    public void IsInsideAttributeValue_UnquotedTerminatorBoundary_ReturnsTrue()
        => IsInsideAttributeValue("<div title=value[|]></div>").ShouldBeTrue();

    [Theory]
    [InlineData("<!-- <div title=\"val[|]ue\"></div> -->")]
    [InlineData("</div title=\"val[|]ue\">")]
    public void IsInsideAttributeValue_IgnoredMarkup_ReturnsFalse(
        string markedTemplate)
        => IsInsideAttributeValue(markedTemplate).ShouldBeFalse();

    [Fact]
    public void IsInsideAttributeValue_EqualsInsideMalformedTagName_ReturnsFalse()
        => IsInsideAttributeValue("<x=[|]y class=\"gap-4\">").ShouldBeFalse();

    private static TemplateClassTokenContext? FindToken(
        string markedTemplate,
        out string templateText)
    {
        var relativePosition = RemoveCaret(markedTemplate, out templateText);
        return TemplateClassTokenScanner.FindTokenAtPosition(
            templateText,
            TemplateStart,
            TemplateStart + relativePosition);
    }

    private static bool IsInsideAttributeValue(string markedTemplate)
    {
        var relativePosition = RemoveCaret(markedTemplate, out var templateText);
        return TemplateClassTokenScanner.IsInsideAttributeValue(
            templateText,
            relativePosition);
    }

    private static int GetDocumentPosition(string markedTemplate)
        => TemplateStart + markedTemplate.IndexOf(CaretMarker, StringComparison.Ordinal);

    private static int RemoveCaret(
        string markedTemplate,
        out string templateText)
    {
        var relativePosition = markedTemplate.IndexOf(CaretMarker, StringComparison.Ordinal);
        relativePosition.ShouldBeGreaterThanOrEqualTo(0);
        markedTemplate.IndexOf(
                CaretMarker,
                relativePosition + CaretMarker.Length,
                StringComparison.Ordinal)
            .ShouldBe(-1, "a scanner test must contain exactly one caret marker");
        templateText = markedTemplate.Remove(relativePosition, CaretMarker.Length);
        return relativePosition;
    }
}
