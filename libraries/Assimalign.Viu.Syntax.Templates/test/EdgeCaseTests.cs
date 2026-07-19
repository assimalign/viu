using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Edge Cases').
public class EdgeCaseTests
{
    [Fact]
    public void Parse_SelfClosingSingleTag_HasNoChildren()
    {
        var root = TestHelpers.Parse("<div :class=\"{ some: condition }\" />");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("div");
        element.Children.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_SelfClosingMultipleTags_ProducesSiblings()
    {
        var root = TestHelpers.Parse("<div :class=\"{ some: condition }\" />\n<p v-bind:style=\"{ color: 'red' }\"/>");

        root.Children.Count.ShouldBe(2);
        root.Children[0].ShouldBeOfType<ElementNode>().Tag.ShouldBe("div");
        root.Children[1].ShouldBeOfType<ElementNode>().Tag.ShouldBe("p");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_ValidHtml_BuildsExpectedTree()
    {
        var root = TestHelpers.Parse(
            "<div :class=\"{ some: condition }\">\n" +
            "  <p v-bind:style=\"{ color: 'red' }\"/>\n" +
            "  <!-- a comment with <html> inside it -->\n" +
            "</div>");

        var div = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        div.Children.Count.ShouldBe(2);
        div.Children[0].ShouldBeOfType<ElementNode>().Tag.ShouldBe("p");
        div.Children[1].ShouldBeOfType<CommentNode>().Content.ShouldBe(" a comment with <html> inside it ");
    }

    [Fact]
    public void Parse_InvalidHtml_RecoversAndReportsErrors()
    {
        // parse.spec.ts 'invalid html': </span> mismatches; the parser recovers per Vue semantics.
        var root = TestHelpers.Parse("<div>\n<span>\n</div>\n</span>", out var errors);

        errors.Count.ShouldBe(2);
        errors[0].Code.ShouldBe(CompilerErrorCode.XMissingEndTag);   // span not closed when </div> seen
        errors[0].Location.Start.ShouldBe(new Position(6, 2, 1));
        errors[1].Code.ShouldBe(CompilerErrorCode.XInvalidEndTag);   // dangling </span>
        errors[1].Location.Start.ShouldBe(new Position(20, 4, 1));
        var div = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        div.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Tag.ShouldBe("span");
    }

    [Fact]
    public void Parse_EndTags_AreCaseInsensitive()
    {
        // parse.spec.ts 'end tags are case-insensitive.'
        var root = TestHelpers.Parse("<div>hello</DIV>after");

        var element = root.Children[0].ShouldBeOfType<ElementNode>();
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("hello");
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe("after");
    }

    [Fact]
    public void Parse_UnclosedTagFollowedByCloseTag_TerminatesForIde()
    {
        // parse.spec.ts 'tag termination handling for IDE': "</" inside an open tag closes it so
        // the following structure stays at root level.
        var root = TestHelpers.Parse("<template><Hello\n</template><script>console.log(1)</script>", out _,
            ParserOptions.CreateHtml());

        root.Children.Count.ShouldBe(2);
        root.Children[1].ShouldBeOfType<ElementNode>().Tag.ShouldBe("script");
    }

    [Fact]
    public void Parse_CdataInForeignContent_BecomesText()
    {
        // CDATA is only valid in non-HTML namespaces (WHATWG); inside <svg> it becomes text.
        var root = TemplateParser.Parse("<svg><![CDATA[some text]]></svg>", ParserOptions.CreateHtml());

        var svg = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        svg.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("some text");
    }

    [Fact]
    public void Parse_EmptyInput_ProducesEmptyRoot()
    {
        var root = TestHelpers.Parse("");

        root.Children.Count.ShouldBe(0);
        root.Source.ShouldBe("");
        root.Location.Source.ShouldBe("");
    }

    [Fact]
    public void Parse_ControlCharacterInput_DoesNotThrow()
    {
        // Control-character input is built programmatically from escapes (repo rule: never write
        // literal control characters into source files).
        var controlCharacters = new string(new[] { (char)0x01, (char)0x02 });
        var source = "<div>" + controlCharacters + "</div>";
        var root = TestHelpers.Parse(source);

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe(controlCharacters);
    }
}
