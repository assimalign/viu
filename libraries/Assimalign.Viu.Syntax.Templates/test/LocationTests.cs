using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Edge Cases')
// 'parse with correct location info' and 'correct loc when the closing > is formatted', plus the
// [V01.01.05.01] acceptance criterion: for EVERY node, loc.Source equals the exact source substring.
public class LocationTests
{
    [Fact]
    public void Parse_MixedTextAndInterpolations_HasExactPositions()
    {
        // parse.spec.ts 'parse with correct location info'
        const string fooSource = "foo\n is ";
        const string barSource = "{{ bar }}";
        const string butSource = " but ";
        const string bazSource = "{{ baz }}";
        var root = TestHelpers.Parse(fooSource + barSource + butSource + bazSource);
        root.Children.Count.ShouldBe(4);

        var offset = 0;
        var foo = root.Children[0].ShouldBeOfType<TextNode>();
        foo.Location.Start.ShouldBe(new Position(offset, 1, 1));
        offset += fooSource.Length;
        foo.Location.End.ShouldBe(new Position(offset, 2, 5));

        var bar = root.Children[1].ShouldBeOfType<InterpolationNode>();
        bar.Location.Start.ShouldBe(new Position(offset, 2, 5));
        var barInner = bar.Content.ShouldBeOfType<SimpleExpressionNode>();
        offset += 3;
        barInner.Location.Start.ShouldBe(new Position(offset, 2, 8));
        offset += 3;
        barInner.Location.End.ShouldBe(new Position(offset, 2, 11));
        offset += 3;
        bar.Location.End.ShouldBe(new Position(offset, 2, 14));

        var but = root.Children[2].ShouldBeOfType<TextNode>();
        but.Location.Start.ShouldBe(new Position(offset, 2, 14));
        offset += butSource.Length;
        but.Location.End.ShouldBe(new Position(offset, 2, 19));

        var baz = root.Children[3].ShouldBeOfType<InterpolationNode>();
        baz.Location.Start.ShouldBe(new Position(offset, 2, 19));
        var bazInner = baz.Content.ShouldBeOfType<SimpleExpressionNode>();
        offset += 3;
        bazInner.Location.Start.ShouldBe(new Position(offset, 2, 22));
        offset += 3;
        bazInner.Location.End.ShouldBe(new Position(offset, 2, 25));
        offset += 3;
        baz.Location.End.ShouldBe(new Position(offset, 2, 28));
    }

    [Fact]
    public void Parse_ClosingTagWithFormattedGreaterThan_ExtendsElementLocation()
    {
        // parse.spec.ts 'correct loc when the closing > is foarmatted' (sic)
        var root = TestHelpers.Parse("<span></span\n      \n      >");

        var span = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        span.Location.Source.ShouldBe("<span></span\n      \n      >");
        span.Location.Start.Offset.ShouldBe(0);
        span.Location.End.Offset.ShouldBe(27);
    }

    [Fact]
    public void Parse_NontrivialTemplate_EveryNodeLocationSourceIsExactSubstring()
    {
        // The [V01.01.05.01] location contract over a template exercising every node kind:
        // elements, attributes, directives (shorthands, dynamic args, modifiers), interpolations,
        // text, comments, nesting, namespaces, and multi-line source.
        const string source =
            "<div id=\"app\" :class=\"cls\" @click.stop=\"go()\">\n" +
            "  <!-- header -->\n" +
            "  <template #header>\n" +
            "    <h1 :[dynamicAttribute]=\"value\">{{ title }} and {{ subtitle }}</h1>\n" +
            "  </template>\n" +
            "  <svg viewBox=\"0 0 1 1\"><foreignObject><p>svg {{ inner }}</p></foreignObject></svg>\n" +
            "  <input v-model.trim=\"name\" disabled>\n" +
            "  plain text &amp; entities\n" +
            "</div>";
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());

        TestHelpers.AssertAllLocationsExact(root);
        root.Location.Source.ShouldBe(source);
    }

    [Fact]
    public void Parse_MultiLineTemplate_TracksLinesAndColumns()
    {
        var root = TestHelpers.Parse("<div>\n  <span>x</span>\n</div>");

        var div = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var span = div.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        span.Location.Start.Line.ShouldBe(2);
        span.Location.Start.Column.ShouldBe(3);
        span.Location.Start.Offset.ShouldBe(8);
        span.Location.End.Line.ShouldBe(2);
        span.Location.End.Column.ShouldBe(17);
    }

    [Fact]
    public void Parse_AttributeNameLocation_CoversNameOnly()
    {
        var root = TestHelpers.Parse("<div some-attribute=\"v\"></div>");

        var attribute = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>();
        attribute.NameLocation.Source.ShouldBe("some-attribute");
        attribute.NameLocation.Start.Offset.ShouldBe(5);
        attribute.NameLocation.End.Offset.ShouldBe(19);
        attribute.Location.Source.ShouldBe("some-attribute=\"v\"");
    }
}
