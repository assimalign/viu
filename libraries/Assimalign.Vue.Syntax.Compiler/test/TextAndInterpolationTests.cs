using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Text') and
// describe('Interpolation').
public class TextAndInterpolationTests
{
    [Fact]
    public void Parse_SimpleText_ProducesSingleTextNode()
    {
        var root = TestHelpers.Parse("some text");

        var text = root.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>();
        text.Content.ShouldBe("some text");
        text.Location.Source.ShouldBe("some text");
        text.Location.Start.Offset.ShouldBe(0);
        text.Location.End.Offset.ShouldBe(9);
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_TextWithInterpolation_SplitsAroundInterpolation()
    {
        var root = TestHelpers.Parse("<div>some {{ foo + bar }} text</div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Children.Count.ShouldBe(3);

        element.Children[0].ShouldBeOfType<TextNode>().Content.ShouldBe("some ");
        var interpolation = element.Children[1].ShouldBeOfType<InterpolationNode>();
        var expression = interpolation.Content.ShouldBeOfType<SimpleExpressionNode>();
        expression.Content.ShouldBe("foo + bar");
        expression.IsStatic.ShouldBeFalse();
        expression.Location.Source.ShouldBe("foo + bar");
        element.Children[2].ShouldBeOfType<TextNode>().Content.ShouldBe(" text");

        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_Interpolation_TrimsInnerWhitespaceButKeepsOuterLocation()
    {
        var root = TestHelpers.Parse("{{ msg }}");

        var interpolation = root.Children.ShouldHaveSingleItem().ShouldBeOfType<InterpolationNode>();
        interpolation.Location.Source.ShouldBe("{{ msg }}");
        var expression = interpolation.Content.ShouldBeOfType<SimpleExpressionNode>();
        expression.Content.ShouldBe("msg");
        expression.Location.Source.ShouldBe("msg");
    }

    [Fact]
    public void Parse_InterpolationWithTagLikeNotation_KeepsAngleBrackets()
    {
        // parse.spec.ts 'it can have tag-like notation'
        var root = TestHelpers.Parse("{{ a<b }}");

        var interpolation = root.Children.ShouldHaveSingleItem().ShouldBeOfType<InterpolationNode>();
        interpolation.Content.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("a<b");
    }

    [Fact]
    public void Parse_LonelyLessThan_DoesNotSeparateNodes()
    {
        // parse.spec.ts 'lonely "<" doesn\'t separate nodes'
        var root = TestHelpers.Parse("a < b");

        var text = root.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>();
        text.Content.ShouldBe("a < b");
        text.Location.Source.ShouldBe("a < b");
    }

    [Fact]
    public void Parse_CustomDelimiters_UsesConfiguredDelimiters()
    {
        // parse.spec.ts 'custom delimiters'
        var options = new ParserOptions { DelimiterOpen = "[[", DelimiterClose = "]]" };
        var root = TemplateParser.Parse("<p>[[ msg ]]</p>", options);

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var interpolation = element.Children.ShouldHaveSingleItem().ShouldBeOfType<InterpolationNode>();
        interpolation.Content.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("msg");
        interpolation.Location.Source.ShouldBe("[[ msg ]]");
    }
}
