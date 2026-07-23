using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Exercises the complete server-render, HTML parse, and Core hydration boundary with both Testing
/// hydration-reader strategies.
/// </summary>
public sealed class ServerRendererHydrationRoundTripTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderedFragment_Hydrate_AdoptsEveryServerNode(
        bool snapshotSemantics)
    {
        IComponent root = ComponentTree.Fragment(
        [
            ComponentTree.Element(
                "section",
                TestTree.Attributes(("class", "panel")),
                [ComponentTree.Text("first")]),
            ComponentTree.Comment("between"),
            ComponentTree.Element(
                "span",
                children: [ComponentTree.Text("second")]),
        ]);
        string html = await ServerRenderer.RenderToStringAsync(root);
        TestElement container = TestServerMarkup.Parse(html);
        TestNode startAnchor = container.Children[0];
        TestNode firstElement = container.Children[1];
        TestNode comment = container.Children[2];
        TestNode secondElement = container.Children[3];
        TestNode endAnchor = container.Children[4];
        TestRenderer renderer =
            new(snapshotSemantics: snapshotSemantics);

        renderer.Hydrate(root, container);

        container.Children[0].ShouldBeSameAs(startAnchor);
        container.Children[1].ShouldBeSameAs(firstElement);
        container.Children[2].ShouldBeSameAs(comment);
        container.Children[3].ShouldBeSameAs(secondElement);
        container.Children[4].ShouldBeSameAs(endAnchor);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root>" + html + "</root>");
        renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderedTemplate_HydrateAndReactiveUpdate_ReusesServerRoot(
        bool snapshotSemantics)
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        Reference<string> message = Reactive.Reference("server");
        InlineComponent component =
            new(
                _ => () =>
                    ComponentTree.Element(
                        "article",
                        children: [ComponentTree.Text(message.Value)]));
        ITemplateComponent request = component.Request();
        ServerApplication application = Ssr.Application(request);
        string html =
            await ServerRenderer.RenderToStringAsync(application);
        TestElement container = TestServerMarkup.Parse(html);
        TestElement serverRoot =
            container.Children[0].ShouldBeOfType<TestElement>();
        TestNode serverText = serverRoot.Children[0];
        TestRenderer renderer =
            new(snapshotSemantics: snapshotSemantics);

        IComponentContext? context =
            renderer.Hydrate(
                request,
                container,
                application.Context);
        pump.RunUntilIdle();

        context.ShouldNotBeNull();
        container.Children[0].ShouldBeSameAs(serverRoot);
        serverRoot.Children[0].ShouldBeSameAs(serverText);
        renderer.OperationLog.Operations.ShouldBeEmpty();
        renderer.OperationLog.Reset();

        message.Value = "client update";
        pump.RunUntilIdle();

        container.Children[0].ShouldBeSameAs(serverRoot);
        serverRoot.Children[0].ShouldBeSameAs(serverText);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><article>client update</article></root>");
        renderer.OperationLog.Count(TestNodeOperationType.SetText)
            .ShouldBe(1);
        renderer.OperationLog.StructuralOperationCount.ShouldBe(0);

        renderer.Render(null, container, application.Context);
        pump.RunUntilIdle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderedTeleport_Hydrate_AdoptsOriginAndTargetRanges(
        bool snapshotSemantics)
    {
        SsrContext context = new();
        IComponent root = ComponentTree.Element(
            "main",
            children:
            [
                ComponentTree.Teleport(
                    "#modal",
                    [
                        ComponentTree.Element(
                            "p",
                            children: [ComponentTree.Text("teleported")]),
                    ]),
            ]);
        string html =
            await ServerRenderer.RenderToStringAsync(root, context);
        TestElement container = TestServerMarkup.Parse(html);
        TestElement serverMain =
            container.Children[0].ShouldBeOfType<TestElement>();
        TestNode originStart = serverMain.Children[0];
        TestNode originEnd = serverMain.Children[1];
        string targetMarkup =
            "<div id=\"modal\">"
            + context.Teleports["#modal"]
            + "</div>";
        TestElement targetDocument =
            TestServerMarkup.Parse(targetMarkup);
        TestElement target =
            targetDocument.Children[0].ShouldBeOfType<TestElement>();
        TestNode targetChild = target.Children[0];
        TestNode targetAnchor = target.Children[1];
        TestRenderer renderer =
            new(snapshotSemantics: snapshotSemantics);
        renderer.RegisterQueryRoot(targetDocument);

        renderer.Hydrate(root, container);

        container.Children[0].ShouldBeSameAs(serverMain);
        serverMain.Children[0].ShouldBeSameAs(originStart);
        serverMain.Children[1].ShouldBeSameAs(originEnd);
        target.Children[0].ShouldBeSameAs(targetChild);
        target.Children[1].ShouldBeSameAs(targetAnchor);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root>" + html + "</root>");
        TestNodeSerializer.Serialize(target)
            .ShouldBe(targetMarkup);
        renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenderedFragment_FrozenHydrationMismatch_ReplacesWholeRangeOnce()
    {
        IComponent serverRoot = ComponentTree.Fragment(
        [
            TestTree.Element("span", "first"),
            TestTree.Element("span", "second"),
        ]);
        string html =
            await ServerRenderer.RenderToStringAsync(serverRoot);
        TestElement container = TestServerMarkup.Parse(html);
        IComponent clientRoot = ComponentTree.Element(
            "div",
            children: [ComponentTree.Text("replacement")]);
        TestRenderer renderer =
            new(snapshotSemantics: true);

        renderer.Hydrate(clientRoot, container);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>replacement</div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.Remove)
            .ShouldBe(4);
        renderer.OperationLog.Count(TestNodeOperationType.CreateElement)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.Insert)
            .ShouldBe(2);
    }
}
