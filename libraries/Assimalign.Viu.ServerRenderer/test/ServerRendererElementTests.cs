using System.Threading.Tasks;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins primitive component-tree serialization to Vue's server-renderer marker and escaping rules.
/// </summary>
public class ServerRendererElementTests
{
    [Fact]
    public async Task Element_WithAttributesAndText_Serializes()
    {
        string html = await Ssr.RenderAsync(
            () => TestTree.Element(
                "div",
                TestTree.Attributes(("id", "app")),
                ComponentTree.Text("hello")));

        html.ShouldBe("<div id=\"app\">hello</div>");
    }

    [Fact]
    public async Task Element_TextChildren_AreEscaped()
    {
        string html = await Ssr.RenderAsync(
            () => TestTree.Element("p", "<b>&amp;</b>"));

        html.ShouldBe("<p>&lt;b&gt;&amp;amp;&lt;/b&gt;</p>");
    }

    [Fact]
    public async Task VoidElement_HasNoClosingTagOrChildren()
    {
        string lineBreak = await Ssr.RenderAsync(
            () => ComponentTree.Element("br"));
        string image = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "img",
                TestTree.Attributes(("src", "a.png"))));

        lineBreak.ShouldBe("<br>");
        image.ShouldBe("<img src=\"a.png\">");
    }

    [Fact]
    public async Task Element_InnerHtml_IsRawUnescaped()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "div",
                TestTree.Attributes(("innerHTML", "<b>bold</b>"))));

        html.ShouldBe("<div><b>bold</b></div>");
    }

    [Fact]
    public async Task Element_TextContent_IsEscapedAndOverridesChildren()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "div",
                TestTree.Attributes(("textContent", "<x>")),
                [ComponentTree.Text("ignored")]));

        html.ShouldBe("<div>&lt;x&gt;</div>");
    }

    [Fact]
    public async Task Textarea_ValueAttribute_BecomesEscapedTextContent()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "textarea",
                TestTree.Attributes(("value", "<hi>"))));

        html.ShouldBe("<textarea>&lt;hi&gt;</textarea>");
    }

    [Fact]
    public async Task Comment_ContentIsSanitized()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Comment("note-->break"));

        html.ShouldBe("<!--notebreak-->");
    }

    [Fact]
    public async Task Static_MarkupIsRaw()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Static("<i>raw</i>"));

        html.ShouldBe("<i>raw</i>");
    }

    [Fact]
    public async Task Fragment_WrapsChildrenInHydrationAnchors()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Fragment(
                [
                    TestTree.Element("span", "a"),
                    TestTree.Element("span", "b"),
                ]));

        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");
    }

    [Fact]
    public async Task Element_NestedChildren_SerializeInOrder()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "ul",
                children:
                [
                    TestTree.Element("li", "one"),
                    TestTree.Element("li", "two"),
                ]));

        html.ShouldBe("<ul><li>one</li><li>two</li></ul>");
    }

    [Fact]
    public async Task Element_BooleanAttribute_RendersByPresence()
    {
        string html = await Ssr.RenderAsync(
            () => ComponentTree.Element(
                "input",
                TestTree.Attributes(("disabled", true), ("readonly", false))));

        html.ShouldBe("<input disabled>");
    }
}
