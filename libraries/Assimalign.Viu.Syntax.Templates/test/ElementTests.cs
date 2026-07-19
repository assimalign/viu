using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Element').
public class ElementTests
{
    [Fact]
    public void Parse_SimpleDiv_ProducesElementWithTextChild()
    {
        var root = TestHelpers.Parse("<div>hello</div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("div");
        element.ElementType.ShouldBe(ElementType.Element);
        element.Namespace.ShouldBe(ElementNamespace.Html);
        element.Location.Source.ShouldBe("<div>hello</div>");
        element.Children.ShouldHaveSingleItem().ShouldBeOfType<TextNode>().Content.ShouldBe("hello");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_EmptyElement_HasNoChildren()
    {
        var root = TestHelpers.Parse("<div></div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Children.Count.ShouldBe(0);
        element.Location.Source.ShouldBe("<div></div>");
    }

    [Fact]
    public void Parse_SelfClosing_ClosesElementAndContinues()
    {
        var root = TestHelpers.Parse("<div/>after");

        var element = root.Children[0].ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("div");
        element.IsSelfClosing.ShouldBeTrue();
        element.Children.Count.ShouldBe(0);
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe("after");
    }

    [Fact]
    public void Parse_VoidElement_ClosesWithoutEndTag()
    {
        var root = TemplateParser.Parse("<img>after", ParserOptions.CreateHtml());

        var element = root.Children[0].ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("img");
        element.Children.Count.ShouldBe(0);
        element.Location.Source.ShouldBe("<img>");
        root.Children[1].ShouldBeOfType<TextNode>().Content.ShouldBe("after");
    }

    [Fact]
    public void Parse_SelfClosingVoidElement_IsSingleElement()
    {
        var root = TemplateParser.Parse("<img/>", ParserOptions.CreateHtml());

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("img");
        element.Location.Source.ShouldBe("<img/>");
    }

    [Fact]
    public void Parse_TemplateWithStructuralDirective_IsTemplateType()
    {
        var root = TemplateParser.Parse("<template v-if=\"ok\"><div/></template>", ParserOptions.CreateHtml());

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Tag.ShouldBe("template");
        element.ElementType.ShouldBe(ElementType.Template);
    }

    [Fact]
    public void Parse_PlainTemplate_IsElementType()
    {
        var root = TestHelpers.Parse("<template><div/></template>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.ElementType.ShouldBe(ElementType.Element);
    }

    [Fact]
    public void Parse_NativeElementWithHtmlOptions_IsElementWhileUnknownIsComponent()
    {
        var html = ParserOptions.CreateHtml();
        TemplateParser.Parse("<div/>", html).Children[0].ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Element);
        TemplateParser.Parse("<foo/>", html).Children[0].ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Component);
    }

    [Fact]
    public void Parse_UpperCaseTag_IsComponent()
    {
        var root = TestHelpers.Parse("<MyComponent/>");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Component);
    }

    [Fact]
    public void Parse_IsCasting_MakesElementComponent()
    {
        // parse.spec.ts 'is casting'
        var root = TestHelpers.Parse("<div is=\"vue:foo\"/>");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Component);
    }

    [Fact]
    public void Parse_SlotElement_IsSlotType()
    {
        var root = TestHelpers.Parse("<slot></slot>");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Slot);
    }

    [Fact]
    public void Parse_CoreComponentTag_IsComponent()
    {
        var root = TestHelpers.Parse("<keep-alive><div/></keep-alive>");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Component);
    }

    [Fact]
    public void Parse_CustomElementOption_KeepsElementType()
    {
        var options = ParserOptions.CreateHtml();
        options.IsCustomElement = tag => tag == "my-widget";
        var root = TemplateParser.Parse("<my-widget/>", options);

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().ElementType.ShouldBe(ElementType.Element);
    }

    [Fact]
    public void Parse_AttributeWithNoValue_HasNullValue()
    {
        var root = TestHelpers.Parse("<div id></div>");

        var attribute = SingleAttribute(root);
        attribute.Name.ShouldBe("id");
        attribute.Value.ShouldBeNull();
        attribute.Location.Source.ShouldBe("id");
        attribute.NameLocation.Source.ShouldBe("id");
    }

    [Fact]
    public void Parse_AttributeWithEmptyDoubleQuotedValue_HasEmptyValueSpanningQuotes()
    {
        var root = TestHelpers.Parse("<div id=\"\"></div>");

        var attribute = SingleAttribute(root);
        attribute.Value!.Content.ShouldBe("");
        attribute.Value.Location.Source.ShouldBe("\"\"");
    }

    [Fact]
    public void Parse_AttributeWithDoubleQuotedValue_KeepsValue()
    {
        var root = TestHelpers.Parse("<div id=\"foo\"></div>");

        var attribute = SingleAttribute(root);
        attribute.Value!.Content.ShouldBe("foo");
        attribute.Value.Location.Source.ShouldBe("\"foo\"");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_AttributeWithSingleQuotedValue_KeepsValue()
    {
        var root = TestHelpers.Parse("<div id='foo'></div>");

        SingleAttribute(root).Value!.Content.ShouldBe("foo");
    }

    [Fact]
    public void Parse_AttributeWithUnquotedValue_KeepsValue()
    {
        var root = TestHelpers.Parse("<div id=foo></div>");

        var attribute = SingleAttribute(root);
        attribute.Value!.Content.ShouldBe("foo");
        attribute.Value.Location.Source.ShouldBe("foo");
    }

    [Fact]
    public void Parse_QuotedAttributeValueWithAngleBracket_KeepsAngleBracket()
    {
        // parse.spec.ts 'attribute value with >'
        var root = TestHelpers.Parse("<div id=\"a>b\"></div>");

        SingleAttribute(root).Value!.Content.ShouldBe("a>b");
    }

    [Fact]
    public void Parse_MultipleAttributes_KeepsOrder()
    {
        var root = TestHelpers.Parse("<div id=\"a\" class=\"b\" title=\"c\"></div>");

        var element = root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>();
        element.Properties.Count.ShouldBe(3);
        element.Properties[0].ShouldBeOfType<AttributeNode>().Name.ShouldBe("id");
        element.Properties[1].ShouldBeOfType<AttributeNode>().Name.ShouldBe("class");
        element.Properties[2].ShouldBeOfType<AttributeNode>().Name.ShouldBe("title");
        TestHelpers.AssertAllLocationsExact(root);
    }

    [Fact]
    public void Parse_ClassAttribute_CondensesWhitespace()
    {
        // parse.spec.ts 'class attribute should ignore whitespace when parsed'
        var root = TestHelpers.Parse("<div class=\"   a    b    \"></div>");

        SingleAttribute(root).Value!.Content.ShouldBe("a b");
    }

    private static AttributeNode SingleAttribute(RootNode root)
        => root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>()
            .Properties.ShouldHaveSingleItem().ShouldBeOfType<AttributeNode>();
}
