using System;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

// Pins the DOM knowledge tables of @vue/shared's domTagConfig.ts/domAttrConfig.ts,
// cross-checked against WHATWG void elements and boolean attributes, plus the
// one-authoritative-definition contract: the netstandard2.0 generator fixture (linked
// shared-source) must agree with the net10.0 frozen tables member-for-member.
public class DomKnowledgeTests
{
    [Theory]
    [InlineData("div")]
    [InlineData("template")]
    [InlineData("blockquote")]
    [InlineData("tfoot")]
    public void IsHtmlTag_KnowsRepresentativeMembers(string tag)
        => DomKnowledge.IsHtmlTag(tag).ShouldBeTrue();

    [Theory]
    [InlineData("my-component")]
    [InlineData("circle")]
    [InlineData("math")]
    public void IsHtmlTag_RejectsNonMembers(string tag)
        => DomKnowledge.IsHtmlTag(tag).ShouldBeFalse();

    [Fact]
    public void IsHtmlTag_IsCaseInsensitivePerWhatwg()
    {
        DomKnowledge.IsHtmlTag("DIV").ShouldBeTrue();
        DomKnowledge.IsHtmlTag("Template").ShouldBeTrue();
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("foreignObject")]
    [InlineData("feGaussianBlur")]
    [InlineData("textPath")]
    [InlineData("animateMotion")]
    public void IsSvgTag_KnowsRepresentativeMembers(string tag)
        => DomKnowledge.IsSvgTag(tag).ShouldBeTrue();

    [Fact]
    public void IsSvgTag_IsCaseSensitivePerSpec()
    {
        // foreignObject is camelCase in SVG; the lower-cased form is not an SVG element.
        DomKnowledge.IsSvgTag("foreignobject").ShouldBeFalse();
        DomKnowledge.IsSvgTag("FEGAUSSIANBLUR").ShouldBeFalse();
        DomKnowledge.IsSvgTag("div").ShouldBeFalse();
    }

    [Theory]
    [InlineData("math")]
    [InlineData("mi")]
    [InlineData("mfrac")]
    [InlineData("annotation-xml")]
    [InlineData("munderover")]
    public void IsMathMLTag_KnowsRepresentativeMembers(string tag)
        => DomKnowledge.IsMathMLTag(tag).ShouldBeTrue();

    [Fact]
    public void IsMathMLTag_RejectsNonMembers()
    {
        DomKnowledge.IsMathMLTag("div").ShouldBeFalse();
        DomKnowledge.IsMathMLTag("svg").ShouldBeFalse();
    }

    [Theory]
    [InlineData("br")]
    [InlineData("img")]
    [InlineData("input")]
    [InlineData("wbr")]
    [InlineData("source")]
    public void IsVoidTag_MatchesWhatwgVoidElements(string tag)
        => DomKnowledge.IsVoidTag(tag).ShouldBeTrue();

    [Theory]
    [InlineData("div")]
    [InlineData("script")]
    [InlineData("template")]
    public void IsVoidTag_RejectsNonVoidElements(string tag)
        => DomKnowledge.IsVoidTag(tag).ShouldBeFalse();

    [Theory]
    [InlineData("checked")]
    [InlineData("disabled")]
    [InlineData("readonly")]
    [InlineData("itemscope")]
    [InlineData("allowfullscreen")]
    [InlineData("inert")]
    public void IsBooleanAttribute_MatchesWhatwgBooleanAttributes(string attribute)
        => DomKnowledge.IsBooleanAttribute(attribute).ShouldBeTrue();

    [Theory]
    [InlineData("title")]
    [InlineData("value")]
    [InlineData("spellcheck")] // enumerated, not boolean
    public void IsBooleanAttribute_RejectsNonBooleanAttributes(string attribute)
        => DomKnowledge.IsBooleanAttribute(attribute).ShouldBeFalse();

    [Theory]
    [InlineData("id")]
    [InlineData("data-testid")]
    [InlineData("aria-label")]
    [InlineData("Custom_Attribute.name:x")]
    public void IsSsrSafeAttributeName_AcceptsSafeNames(string name)
        => DomKnowledge.IsSsrSafeAttributeName(name).ShouldBeTrue();

    [Theory]
    [InlineData("bad>name")]
    [InlineData("bad/name")]
    [InlineData("bad=name")]
    [InlineData("bad\"name")]
    [InlineData("bad'name")]
    [InlineData("bad name")]
    [InlineData("bad\tname")]
    [InlineData("bad\nname")]
    [InlineData("")]
    public void IsSsrSafeAttributeName_RejectsUnsafeNames(string name)
        => DomKnowledge.IsSsrSafeAttributeName(name).ShouldBeFalse();

    [Fact]
    public void GetAttributeName_CoversTheUpstreamCasingExceptions()
    {
        DomKnowledge.GetAttributeName("acceptCharset").ShouldBe("accept-charset");
        DomKnowledge.GetAttributeName("className").ShouldBe("class");
        DomKnowledge.GetAttributeName("htmlFor").ShouldBe("for");
        DomKnowledge.GetAttributeName("httpEquiv").ShouldBe("http-equiv");
        DomKnowledge.GetAttributeName("id").ShouldBe("id"); // passthrough
    }

    [Theory]
    [InlineData("class")]
    [InlineData("http-equiv")]
    [InlineData("tabindex")]
    [InlineData("srcset")]
    public void IsKnownHtmlAttribute_KnowsRepresentativeMembers(string attribute)
        => DomKnowledge.IsKnownHtmlAttribute(attribute).ShouldBeTrue();

    [Theory]
    [InlineData("viewBox")]
    [InlineData("stroke-width")]
    [InlineData("xlink:href")]
    [InlineData("preserveAspectRatio")]
    public void IsKnownSvgAttribute_KnowsRepresentativeMembers(string attribute)
        => DomKnowledge.IsKnownSvgAttribute(attribute).ShouldBeTrue();

    [Fact]
    public void IsKnownSvgAttribute_RejectsUnknownNames()
        => DomKnowledge.IsKnownSvgAttribute("not-an-svg-attr").ShouldBeFalse();

    // --- span alternate lookups: allocation-free membership for compiler tokenization -------

    [Fact]
    public void SpanOverloads_AgreeWithTheStringLookups()
    {
        // The compiler tests tag/attribute tokens as spans over source text; both surfaces
        // must answer identically (same frozen sets through alternate lookups).
        DomKnowledge.IsHtmlTag("div".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsHtmlTag("DIV".AsSpan()).ShouldBeTrue(); // case-insensitive holds for spans
        DomKnowledge.IsHtmlTag("my-component".AsSpan()).ShouldBeFalse();
        DomKnowledge.IsSvgTag("foreignObject".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsSvgTag("foreignobject".AsSpan()).ShouldBeFalse(); // case-sensitive holds
        DomKnowledge.IsMathMLTag("annotation-xml".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsVoidTag("br".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsBooleanAttribute("checked".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsKnownHtmlAttribute("tabindex".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsKnownSvgAttribute("viewBox".AsSpan()).ShouldBeTrue();
    }

    [Fact]
    public void SpanOverloads_MatchSlicedTokens_WithoutMaterializingStrings()
    {
        // A token sliced out of larger source text, never turned into a string.
        var source = "<template><div class=\"x\"></div></template>".AsSpan();
        DomKnowledge.IsHtmlTag(source.Slice(1, 8)).ShouldBeTrue();  // "template"
        DomKnowledge.IsHtmlTag(source.Slice(11, 3)).ShouldBeTrue(); // "div"
    }

    [Fact]
    public void TryGetAttributeName_MapsExceptionsAndReportsPassThroughs()
    {
        DomKnowledge.TryGetAttributeName("htmlFor".AsSpan(), out var mapped).ShouldBeTrue();
        mapped.ShouldBe("for");
        DomKnowledge.TryGetAttributeName("className".AsSpan(), out mapped).ShouldBeTrue();
        mapped.ShouldBe("class");
        DomKnowledge.TryGetAttributeName("id".AsSpan(), out _).ShouldBeFalse();
    }

    [Fact]
    public void IsSsrSafeAttributeName_SpanAndStringSurfacesAgree_IncludingControlCharacters()
    {
        DomKnowledge.IsSsrSafeAttributeName("data-x".AsSpan()).ShouldBeTrue();
        DomKnowledge.IsSsrSafeAttributeName("bad=name".AsSpan()).ShouldBeFalse();
        DomKnowledge.IsSsrSafeAttributeName("bad\u0001name").ShouldBeFalse(); // C0 control char
        DomKnowledge.IsSsrSafeAttributeName("bad\u0001name".AsSpan()).ShouldBeFalse();
        DomKnowledge.IsSsrSafeAttributeName(default(ReadOnlySpan<char>)).ShouldBeFalse(); // empty
    }

    // --- generator-fixture parity: both consumption paths read one data definition ----------

    [Fact]
    public void GeneratorFixture_HtmlTags_AgreeWithTheRuntimeTables()
    {
        foreach (var tag in GeneratorHostDomKnowledge.HtmlTags)
        {
            DomKnowledge.IsHtmlTag(tag).ShouldBeTrue($"runtime tables miss HTML tag '{tag}'");
        }
        GeneratorHostDomKnowledge.IsHtmlTag("div").ShouldBeTrue();
        GeneratorHostDomKnowledge.IsHtmlTag("DIV").ShouldBeTrue();
        GeneratorHostDomKnowledge.IsHtmlTag("circle").ShouldBeFalse();
    }

    [Fact]
    public void GeneratorFixture_SvgAndVoidTags_AgreeWithTheRuntimeTables()
    {
        foreach (var tag in GeneratorHostDomKnowledge.SvgTags)
        {
            DomKnowledge.IsSvgTag(tag).ShouldBeTrue($"runtime tables miss SVG tag '{tag}'");
        }
        foreach (var tag in GeneratorHostDomKnowledge.VoidTags)
        {
            DomKnowledge.IsVoidTag(tag).ShouldBeTrue($"runtime tables miss void tag '{tag}'");
        }
        GeneratorHostDomKnowledge.IsSvgTag("foreignObject").ShouldBeTrue();
        GeneratorHostDomKnowledge.IsSvgTag("foreignobject").ShouldBeFalse();
        GeneratorHostDomKnowledge.IsVoidTag("br").ShouldBeTrue();
    }

    [Fact]
    public void GeneratorFixture_BooleanAttributes_AgreeWithTheRuntimeTables()
    {
        foreach (var attribute in GeneratorHostDomKnowledge.BooleanAttributes)
        {
            DomKnowledge.IsBooleanAttribute(attribute)
                .ShouldBeTrue($"runtime tables miss boolean attribute '{attribute}'");
        }
    }
}
