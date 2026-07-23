using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins that server output parses into the exact element/comment vocabulary consumed by Core
/// hydration readers.
/// </summary>
public class ServerRendererMarkerContractTests
{
    [Fact]
    public async Task SingleRootTree_ServerMarkupParser_PreservesElementTextAndAttributes()
    {
        IComponent root = ComponentTree.Element(
            "div",
            TestTree.Attributes(("id", "app")),
            [ComponentTree.Text("hello")]);

        string html = await ServerRenderer.RenderToStringAsync(root);
        TestElement container = TestServerMarkup.Parse(html);

        TestElement element = container.Children[0].ShouldBeOfType<TestElement>();
        element.Tag.ShouldBe("div");
        element.Properties["id"].ShouldBe("app");
        element.Children[0].ShouldBeOfType<TestText>().Text.ShouldBe("hello");
    }

    [Fact]
    public async Task Fragment_ServerMarkupParser_PreservesHydrationAnchorComments()
    {
        IComponent root = ComponentTree.Fragment(
            [
                TestTree.Element("span", "a"),
                TestTree.Element("span", "b"),
            ]);

        string html = await ServerRenderer.RenderToStringAsync(root);
        TestElement container = TestServerMarkup.Parse(html);

        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");
        container.Children.Count.ShouldBe(4);
        container.Children[0].ShouldBeOfType<TestComment>().Text.ShouldBe("[");
        container.Children[3].ShouldBeOfType<TestComment>().Text.ShouldBe("]");
    }
}
