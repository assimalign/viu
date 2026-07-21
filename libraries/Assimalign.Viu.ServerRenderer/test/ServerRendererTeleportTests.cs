using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Teleport SSR: the origin gets the start/end anchor pair while content is buffered by target selector
/// into <see cref="SsrContext.Teleports"/> — pinned to upstream <c>ssrRenderTeleport</c> and the
/// <c>ssrContext.teleports</c> contract the hydration walker consumes.
/// </summary>
public class ServerRendererTeleportTests
{
    private static InlineComponent TeleportHost(VirtualNodeProperties? teleportProperties, params VirtualNode?[] children) =>
        new((_, _) => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Teleport(teleportProperties, children)));

    [Fact]
    public async Task Enabled_BuffersContentByTarget_AndLeavesAnchorPairInPlace()
    {
        var context = new SsrContext();
        var component = TeleportHost(
            VirtualNodeFactory.Properties(("to", "#modal")),
            VirtualNodeFactory.Element("p", "hi"));

        var html = await ServerRenderer.RenderToStringAsync(component, null, context);

        html.ShouldBe("<div><!--teleport start--><!--teleport end--></div>");
        context.Teleports["#modal"].ShouldBe("<p>hi</p><!--teleport anchor-->");
    }

    [Fact]
    public async Task Disabled_RendersContentInPlace_AndBuffersOnlyAnchor()
    {
        var context = new SsrContext();
        var component = TeleportHost(
            VirtualNodeFactory.Properties(("to", "#modal"), ("disabled", true)),
            VirtualNodeFactory.Element("p", "hi"));

        var html = await ServerRenderer.RenderToStringAsync(component, null, context);

        html.ShouldBe("<div><!--teleport start--><p>hi</p><!--teleport end--></div>");
        context.Teleports["#modal"].ShouldBe("<!--teleport anchor-->");
    }

    [Fact]
    public async Task MissingTarget_SkipsContent_AndBuffersNothing()
    {
        var context = new SsrContext();
        var component = TeleportHost(
            VirtualNodeFactory.Properties(("disabled", false)),
            VirtualNodeFactory.Element("p", "hi"));

        var html = await ServerRenderer.RenderToStringAsync(component, null, context);

        html.ShouldBe("<div><!--teleport start--><!--teleport end--></div>");
        context.Teleports.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MultipleTeleports_SameTarget_AccumulateInOrder()
    {
        var context = new SsrContext();
        var component = new InlineComponent((_, _) => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Teleport(VirtualNodeFactory.Properties(("to", "#modal")), [VirtualNodeFactory.Element("p", "a")]),
            VirtualNodeFactory.Teleport(VirtualNodeFactory.Properties(("to", "#modal")), [VirtualNodeFactory.Element("p", "b")])));

        await ServerRenderer.RenderToStringAsync(component, null, context);

        context.Teleports["#modal"].ShouldBe("<p>a</p><!--teleport anchor--><p>b</p><!--teleport anchor-->");
    }
}
