using Shouldly;

using Xunit;

namespace Assimalign.Vue.Compiler;

// Raw-text (script/style) and RCDATA (title/textarea) handling in HTML mode, per
// @vue/compiler-dom and the WHATWG spec.
public class RawTextAndRcdataTests
{
    private static RootNode ParseHtml(string source) => TemplateParser.Parse(source, ParserOptions.CreateHtml());

    [Fact]
    public void Parse_ScriptRawText_KeepsAngleBracketsAsText()
    {
        var script = ParseHtml("<script>if (a < b) { x() }</script>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        script.Tag.ShouldBe("script");
        script.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("if (a < b) { x() }");
    }

    [Fact]
    public void Parse_ScriptRawText_DoesNotDecodeEntities()
    {
        var script = ParseHtml("<script>a &amp; b</script>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        var text = script.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>();
        text.Content.ShouldBe("a &amp; b");
        text.Location.Source.ShouldBe("a &amp; b");
    }

    [Fact]
    public void Parse_StyleRawText_KeepsContentVerbatim()
    {
        var style = ParseHtml("<style>.a { color: red; }</style>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        style.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe(".a { color: red; }");
    }

    [Fact]
    public void Parse_ScriptWithEndTagInText_EndsAtScriptClose()
    {
        var script = ParseHtml("<script>x</div>y</script>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        script.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("x</div>y");
    }

    [Fact]
    public void Parse_TextareaRcdata_DecodesEntities()
    {
        var textarea = ParseHtml("<textarea>a &amp; b</textarea>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        var text = textarea.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>();
        text.Content.ShouldBe("a & b");
        text.Location.Source.ShouldBe("a &amp; b");
    }

    [Fact]
    public void Parse_TextareaRcdata_ParsesInterpolation()
    {
        var textarea = ParseHtml("<textarea>{{ value }}</textarea>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        var interpolation = textarea.Children.ShouldHaveSingleItem().ShouldBeOfType<InterpolationNode>();
        interpolation.Content.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("value");
    }

    [Fact]
    public void Parse_TitleRcdata_DecodesEntities()
    {
        var title = ParseHtml("<title>Tom &amp; Jerry</title>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        title.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("Tom & Jerry");
    }

    [Fact]
    public void Parse_TextareaRcdata_PreservesInnerWhitespace()
    {
        // Whitespace condensing is skipped for RCDATA content (tokenizer.inRCDATA).
        var textarea = ParseHtml("<textarea>  spaced   text  </textarea>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        textarea.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("  spaced   text  ");
    }

    [Fact]
    public void Parse_AfterSfcRawTextThenBaseParse_DoesNotThrow()
    {
        // parse.spec.ts 'should reset inRCDATA state': a fresh parse is unaffected by a prior SFC parse.
        var sfcOptions = new ParserOptions { Mode = TemplateParseMode.Sfc, OnError = _ => { } };
        TemplateParser.Parse("<Foo>", sfcOptions);

        var root = TestHelpers.Parse("{ foo }");
        root.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("{ foo }");
    }
}
