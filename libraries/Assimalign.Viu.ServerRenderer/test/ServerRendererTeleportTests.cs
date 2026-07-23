using System.Threading.Tasks;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins teleport origin markers and per-target buffering for the unified component tree.
/// </summary>
public class ServerRendererTeleportTests
{
    [Fact]
    public async Task Enabled_BuffersContentByTarget_AndLeavesAnchorPairInPlace()
    {
        SsrContext context = new();
        IComponent root = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Teleport(
                    "#modal",
                    [TestTree.Element("p", "hi")]),
            ]);

        string html = await ServerRenderer.RenderToStringAsync(root, context);

        html.ShouldBe("<div><!--teleport start--><!--teleport end--></div>");
        context.Teleports["#modal"].ShouldBe("<p>hi</p><!--teleport anchor-->");
    }

    [Fact]
    public async Task Disabled_RendersContentInPlace_AndBuffersOnlyAnchor()
    {
        SsrContext context = new();
        IComponent root = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Teleport(
                    "#modal",
                    [TestTree.Element("p", "hi")],
                    isDisabled: true),
            ]);

        string html = await ServerRenderer.RenderToStringAsync(root, context);

        html.ShouldBe("<div><!--teleport start--><p>hi</p><!--teleport end--></div>");
        context.Teleports["#modal"].ShouldBe("<!--teleport anchor-->");
    }

    [Fact]
    public async Task NonStringTarget_SkipsContent_AndBuffersNothing()
    {
        SsrContext context = new();
        IComponent root = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Teleport(
                    new object(),
                    [TestTree.Element("p", "hi")]),
            ]);

        string html = await ServerRenderer.RenderToStringAsync(root, context);

        html.ShouldBe("<div><!--teleport start--><!--teleport end--></div>");
        context.Teleports.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MultipleTeleports_SameTarget_AccumulateInOrder()
    {
        SsrContext context = new();
        IComponent root = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Teleport("#modal", [TestTree.Element("p", "a")]),
                ComponentTree.Teleport("#modal", [TestTree.Element("p", "b")]),
            ]);

        await ServerRenderer.RenderToStringAsync(root, context);

        context.Teleports["#modal"].ShouldBe(
            "<p>a</p><!--teleport anchor--><p>b</p><!--teleport anchor-->");
    }
}
