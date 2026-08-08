using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Namespace inference per the WHATWG tree-construction dispatcher:
// https://html.spec.whatwg.org/multipage/parsing.html#tree-construction-dispatcher.
public class NamespaceTests
{
    private static RootNode ParseHtml(string source) => TemplateParser.Parse(source, ParserOptions.CreateHtml());

    [Fact]
    public void Parse_Svg_SwitchesChildrenToSvgNamespace()
    {
        var svg = ParseHtml("<svg><rect/></svg>").Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        svg.Namespace.ShouldBe(ElementNamespace.Svg);
        svg.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Namespace.ShouldBe(ElementNamespace.Svg);
    }

    [Fact]
    public void Parse_MathMl_SwitchesChildrenToMathMlNamespace()
    {
        var math = ParseHtml("<math><mi/></math>").Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        math.Namespace.ShouldBe(ElementNamespace.MathMl);
        math.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Namespace.ShouldBe(ElementNamespace.MathMl);
    }

    [Fact]
    public void Parse_SvgForeignObject_ReturnsChildrenToHtml()
    {
        var svg = ParseHtml("<svg><foreignObject><div/></foreignObject></svg>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var foreignObject = svg.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var div = foreignObject.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        foreignObject.Namespace.ShouldBe(ElementNamespace.Svg);
        div.Namespace.ShouldBe(ElementNamespace.Html);
    }

    [Fact]
    public void Parse_MathMlTextIntegrationPoint_ReturnsChildrenToHtml()
    {
        var math = ParseHtml("<math><mtext><div/></mtext></math>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var mtext = math.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var div = mtext.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        mtext.Namespace.ShouldBe(ElementNamespace.MathMl);
        div.Namespace.ShouldBe(ElementNamespace.Html);
    }

    [Fact]
    public void Parse_AnnotationXmlWithSvgChild_SwitchesToSvg()
    {
        var math = ParseHtml("<math><annotation-xml><svg/></annotation-xml></math>")
            .Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var annotation = math.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        var svg = annotation.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();

        annotation.Namespace.ShouldBe(ElementNamespace.MathMl);
        svg.Namespace.ShouldBe(ElementNamespace.Svg);
    }

    [Fact]
    public void Parse_SiblingAfterSvg_ReturnsToHtml()
    {
        var root = ParseHtml("<svg/><div/>");

        root.Children[0].ShouldBeOfType<ElementNode>().Namespace.ShouldBe(ElementNamespace.Svg);
        root.Children[1].ShouldBeOfType<ElementNode>().Namespace.ShouldBe(ElementNamespace.Html);
    }
}
