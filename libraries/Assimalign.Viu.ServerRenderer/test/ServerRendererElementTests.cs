using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// End-to-end serialization of the element/text/comment/fragment/static vnode kinds through the
/// runtime renderer, pinned to <c>@vue/server-renderer/src/render.ts</c> — including the void-element,
/// child-override, and hydration-marker rules.
/// </summary>
public class ServerRendererElementTests
{
    [Fact]
    public async Task Element_WithAttributesAndText_Serializes()
    {
        var html = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("id", "app")), "hello"));
        html.ShouldBe("<div id=\"app\">hello</div>");
    }

    [Fact]
    public async Task Element_TextChildren_AreEscaped()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Element("p", "<b>&amp;</b>"));
        html.ShouldBe("<p>&lt;b&gt;&amp;amp;&lt;/b&gt;</p>");
    }

    [Fact]
    public async Task VoidElement_HasNoClosingTagOrChildren()
    {
        var html = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("br"));
        html.ShouldBe("<br>");

        var image = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("img", VirtualNodeFactory.Properties(("src", "a.png"))));
        image.ShouldBe("<img src=\"a.png\">");
    }

    [Fact]
    public async Task Element_InnerHtml_IsRawUnescaped()
    {
        // v-html surfaces as the innerHTML prop and is written verbatim (the documented raw-HTML path).
        var html = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("innerHTML", "<b>bold</b>")), (string)null!));
        html.ShouldBe("<div><b>bold</b></div>");
    }

    [Fact]
    public async Task Element_TextContent_IsEscapedAndOverridesChildren()
    {
        var html = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("textContent", "<x>")), "ignored"));
        html.ShouldBe("<div>&lt;x&gt;</div>");
    }

    [Fact]
    public async Task Textarea_ValueProp_BecomesEscapedTextContent()
    {
        var html = await Ssr.RenderAsync(() =>
            VirtualNodeFactory.Element("textarea", VirtualNodeFactory.Properties(("value", "<hi>")), (string)null!));
        html.ShouldBe("<textarea>&lt;hi&gt;</textarea>");
    }

    [Fact]
    public async Task Comment_ContentIsSanitized()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Comment("note-->break"));
        html.ShouldBe("<!--notebreak-->");
    }

    [Fact]
    public async Task Static_MarkupIsRaw()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Static("<i>raw</i>"));
        html.ShouldBe("<i>raw</i>");
    }

    [Fact]
    public async Task Fragment_WrapsChildrenInHydrationAnchors()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Fragment(
            VirtualNodeFactory.Element("span", "a"),
            VirtualNodeFactory.Element("span", "b")));
        // Upstream fragment markers <!--[--> ... <!--]--> bound the child range for hydration.
        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");
    }

    [Fact]
    public async Task Element_NestedChildren_SerializeInOrder()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Element(
            "ul",
            VirtualNodeFactory.Element("li", "one"),
            VirtualNodeFactory.Element("li", "two")));
        html.ShouldBe("<ul><li>one</li><li>two</li></ul>");
    }

    [Fact]
    public async Task Element_BooleanAttribute_RendersByPresence()
    {
        var html = await Ssr.RenderAsync(() => VirtualNodeFactory.Element(
            "input",
            VirtualNodeFactory.Properties(("disabled", true), ("readonly", false))));
        html.ShouldBe("<input disabled>");
    }
}
