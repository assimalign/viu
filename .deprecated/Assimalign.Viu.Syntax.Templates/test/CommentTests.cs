using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Comment').
public class CommentTests
{
    [Fact]
    public void Parse_EmptyComment_HasEmptyContent()
    {
        var root = TestHelpers.Parse("<!---->");

        var comment = root.Children.ShouldHaveSingleItem().ShouldBeOfType<CommentNode>();
        comment.Content.ShouldBe("");
        comment.Location.Source.ShouldBe("<!---->");
    }

    [Fact]
    public void Parse_SimpleComment_KeepsBodyAndDelimiters()
    {
        var root = TestHelpers.Parse("<!--foo-->");

        var comment = root.Children.ShouldHaveSingleItem().ShouldBeOfType<CommentNode>();
        comment.Content.ShouldBe("foo");
        comment.Location.Source.ShouldBe("<!--foo-->");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_TwoComments_ProducesTwoNodes()
    {
        var root = TestHelpers.Parse("<!--foo--><!--bar-->");

        root.Children.Count.ShouldBe(2);
        root.Children[0].ShouldBeOfType<CommentNode>().Content.ShouldBe("foo");
        root.Children[1].ShouldBeOfType<CommentNode>().Content.ShouldBe("bar");
    }

    [Fact]
    public void Parse_CommentsDisabled_DropsCommentNodes()
    {
        // parse.spec.ts 'comments option'
        var options = new ParserOptions { KeepComments = false };
        var root = TemplateParser.Parse("<div><!--foo--><span/></div>", options);

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Tag.ShouldBe("span");
    }
}
