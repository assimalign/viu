using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts,
// describe('whitespace management when adopting strategy condense') and ('... strategy preserve').
public class WhitespaceTests
{
    private static RootNode ParseCondense(string source) => TestHelpers.Parse(source);

    private static RootNode ParsePreserve(string source)
        => TemplateParser.Parse(source, new ParserOptions { Whitespace = WhitespaceStrategy.Preserve });

    private static RootNode ParseHtml(string source, WhitespaceStrategy strategy = WhitespaceStrategy.Condense)
    {
        var options = ParserOptions.CreateHtml();
        options.Whitespace = strategy;
        return TemplateParser.Parse(source, options);
    }

    [Fact]
    public void Condense_RemovesWhitespaceAtStartAndEndInsideElement()
    {
        var element = ParseCondense("<div>   <span/>    </div>").Children[0].ShouldBeOfType<ElementNode>();

        element.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Tag.ShouldBe("span");
    }

    [Fact]
    public void Condense_RemovesNewlineWhitespaceBetweenElements()
    {
        var element = ParseCondense("<div/> \n <div/> \n <div/>");

        element.Children.Count.ShouldBe(3);
        element.Children.ShouldAllBe(child => child is ElementNode);
    }

    [Fact]
    public void Condense_RemovesWhitespaceAdjacentToComments()
    {
        var root = ParseCondense("<div/> \n <!--foo--> <div/>");

        root.Children.Count.ShouldBe(3);
        root.Children[0].ShouldBeOfType<ElementNode>();
        root.Children[1].ShouldBeOfType<CommentNode>();
        root.Children[2].ShouldBeOfType<ElementNode>();
    }

    [Fact]
    public void Condense_KeepsSpaceBetweenElementsWithoutNewline()
    {
        // 'should NOT remove whitespaces w/o newline between elements'
        var root = ParseCondense("<div/> <div/> <div/>");

        root.Children.Count.ShouldBe(5);
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe(" ");
        root.Children[3].ShouldBeOfType<TextNode>().Content.ShouldBe(" ");
    }

    [Fact]
    public void Condense_KeepsNewlineWhitespaceBetweenInterpolations()
    {
        // 'should NOT remove whitespaces w/ newline between interpolations'
        var root = ParseCondense("{{ a }} \n {{ b }}");

        root.Children.Count.ShouldBe(3);
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe(" ");
    }

    [Fact]
    public void Condense_CondensesConsecutiveWhitespaceInText()
    {
        var root = ParseCondense("   foo  \n    bar     baz     ");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe(" foo bar baz ");
    }

    [Fact]
    public void Condense_RemovesLeadingNewlineInPre()
    {
        // 'should remove leading newline character immediately following the pre element start tag'
        // (WHATWG: https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody)
        var root = ParseHtml("<pre>\n  foo  bar  </pre>");

        var pre = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        pre.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("  foo  bar  ");
    }

    [Fact]
    public void Condense_PreservesWhitespaceInsidePre()
    {
        var root = ParseHtml("<pre>  a   b\n   c</pre>");

        var pre = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        pre.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("  a   b\n   c");
    }

    [Fact]
    public void Preserve_StillRemovesLeadTrailInsideElementButKeepsBetweenElements()
    {
        // preserve strategy: lone lead/trail whitespace nodes are still dropped...
        var element = ParsePreserve("<div>   <span/>    </div>").Children[0].ShouldBeOfType<ElementNode>();
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        // ...but whitespace between elements (even with newline) is kept — condensed to one space,
        // matching upstream's 'should preserve whitespaces w/ newline between interpolations'.
        var root = ParsePreserve("<div/> \n <div/>");
        root.Children.Count.ShouldBe(3);
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe(" ");
    }

    [Fact]
    public void Preserve_KeepsConsecutiveWhitespaceInText()
    {
        var root = ParsePreserve("<div>foo  \n    bar</div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("foo  \n    bar");
    }
}
