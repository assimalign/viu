using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>Tests teleport behavior through browser-shaped integer node handles.</summary>
[SupportedOSPlatform("browser")]
public sealed class TeleportDomTests
{
    [Fact]
    public void Teleport_MovesChildrenIntoDirectTargetHandle()
    {
        InMemoryHandleDom dom = new();
        using DirectHandleDomWorld world = new(dom);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(world.Options);
        int container = dom.CreateElement("root", null);
        int target = dom.CreateElement("target", null);

        renderer.Render(
            ComponentTree.Teleport(
                target,
                [ComponentTree.Element(
                    "span",
                    children: [ComponentTree.Text("boxed")])]),
            container);

        dom.Serialize(target)
            .ShouldBe("<target><span>boxed</span></target>");
        dom.Serialize(container)
            .ShouldBe(
                "<root><!--teleport start--><!--teleport end--></root>");
    }

    [Fact]
    public void Teleport_UnresolvedStringTarget_DoesNotThrow()
    {
        InMemoryHandleDom dom = new();
        using DirectHandleDomWorld world = new(dom);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(world.Options);
        int container = dom.CreateElement("root", null);

        Should.NotThrow(
            () => renderer.Render(
                ComponentTree.Teleport(
                    "#anywhere",
                    [ComponentTree.Element(
                        "span",
                        children: [ComponentTree.Text("x")])]),
                container));

        dom.Serialize(container)
            .ShouldBe(
                "<root><!--teleport start--><!--teleport end--></root>");
    }

    [Fact]
    public void BufferedOperations_ResolveSelectorAndReserveForeignHandle()
    {
        const int target = 12;
        InMemoryHandleDom dom = new();
        BufferedBrowserNodeOperations buffered = new(
            static (_, _) => [],
            selector =>
            {
                selector.ShouldBe("#target");
                return target;
            },
            dom.ParentNode,
            dom.NextSibling,
            dom.InsertStaticContent);
        RendererOptions<int> options = buffered.Create();

        int resolved =
            options.ResolveTeleportTarget!.Invoke("#target");
        int created = options.CreateElement("div", null);

        resolved.ShouldBe(target);
        created.ShouldBeGreaterThan(target);
    }
}
