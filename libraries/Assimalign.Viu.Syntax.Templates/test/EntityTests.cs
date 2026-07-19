using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts describe('decodeEntities option'),
// exercising the embedded WHATWG table through the parser (content decoded, loc.Source raw).
public class EntityTests
{
    [Fact]
    public void Parse_KnownAndUnknownNamedReferences_DecodesKnownOnly()
    {
        // parse.spec.ts 'use decode by default': &foo; has no table entry and stays literal.
        var root = TestHelpers.Parse("&gt;&lt;&amp;&apos;&quot;&foo;");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("><&'\"&foo;");
    }

    [Fact]
    public void Parse_NumericReferences_DecodeDecimalAndHex()
    {
        // &#65; = 'A' (decimal), &#x41; = 'A' (hex), &#X42; = 'B' (hex, capital X).
        TestHelpers.Parse("&#65;&#x41;&#X42;").Children.ShouldHaveSingleItem()
            .ShouldBeOfType<TextNode>().Content.ShouldBe("AAB");
    }

    [Fact]
    public void Parse_MultiCodePointReference_DecodesToTwoChars()
    {
        // &NotEqualTilde; -> U+2242 U+0338
        TestHelpers.Parse("&NotEqualTilde;").Children.ShouldHaveSingleItem()
            .ShouldBeOfType<TextNode>().Content.ShouldBe("≂̸");
    }

    [Fact]
    public void Parse_TextEntity_DecodesContentButKeepsRawSource()
    {
        var root = TestHelpers.Parse("<div>a &amp; b</div>");

        var text = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>();
        text.Content.ShouldBe("a & b");
        text.Location.Source.ShouldBe("a &amp; b");
    }

    [Fact]
    public void Parse_AttributeEntity_DecodesValue()
    {
        var root = TestHelpers.Parse("<div title=\"&copy; 2026\"></div>");

        var attribute = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>();
        attribute.Value!.Content.ShouldBe("© 2026");
    }

    [Fact]
    public void Parse_LegacyAmpersandInAttribute_IsNotDecodedBeforeEquals()
    {
        // WHATWG ambiguous-ampersand rule: "&amp=" in an attribute stays literal.
        var root = TestHelpers.Parse("<a href=\"?x=1&amp=2\"></a>");

        var attribute = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>();
        attribute.Value!.Content.ShouldBe("?x=1&amp=2");
    }

    [Fact]
    public void Parse_LegacyAmpersandInText_IsDecoded()
    {
        // In text (not attribute) the legacy no-semicolon "&amp" decodes.
        TestHelpers.Parse("x&ampy").Children.ShouldHaveSingleItem()
            .ShouldBeOfType<TextNode>().Content.ShouldBe("x&y");
    }
}
