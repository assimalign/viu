using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.ServerRenderer.Tests;

// End-to-end proof that the SSR marker output ([V01.01.07.01]) is exactly the contract the hydration
// walker ([V01.01.07.03]) consumes: render a component to HTML with the server renderer, parse that HTML
// into the in-memory tree, and hydrate the SAME component against it — adoption must be mutation-free and
// reactive updates must patch the adopted nodes in place. This couples the two ends without the walker
// taking a code dependency on the server renderer (issue #66 boundary): only the emitted byte sequences.
public class HydrationRoundTripTests
{
    [Fact]
    public async Task SingleRootComponent_ServerRenderedThenHydrated_AdoptsAndUpdatesInPlace()
    {
        var message = Reactive.Reference("hello");
        InlineComponent Component() => new((_, _) => () =>
            VirtualNodeFactory.Element(
                "div", VirtualNodeFactory.Properties(("id", "app")), message.Value, PatchFlags.Text));

        // 1. Render on the server.
        var html = await Ssr.RenderAsync(Component());
        html.ShouldBe("<div id=\"app\">hello</div>");

        // 2. Parse the server HTML into the in-memory tree and hydrate the same component over it.
        Scheduler.Reset();
        using var pump = TestSchedulerPump.Install();
        var container = TestServerMarkup.Parse(html);
        var serverDiv = container.Children[0];
        var renderer = new TestRenderer();
        var root = VirtualNodeFactory.Component(Component());
        renderer.Hydrate(root, container);

        // Adoption is mutation-free — the server div is reused.
        var log = renderer.OperationLog;
        log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        log.Count(TestNodeOperationType.SetElementText).ShouldBe(0);

        // 3. A reactive update patches the adopted node in place — no remount.
        log.Reset();
        message.Value = "world";
        pump.RunUntilIdle();

        log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        log.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        container.Children[0].ShouldBeSameAs(serverDiv);
        ((TestText)((TestElement)serverDiv).Children[0]).Text.ShouldBe("world");
        Scheduler.Reset();
    }

    [Fact]
    public async Task MultiRootComponent_ServerRenderedFragment_HydratesViaAnchorComments()
    {
        InlineComponent Component() => new((_, _) => () =>
            VirtualNodeFactory.Fragment(
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("span", "a"),
                    VirtualNodeFactory.Element("span", "b"),
                },
                null,
                PatchFlags.StableFragment));

        // The server wraps a multi-root component subtree in the fragment markers.
        var html = await Ssr.RenderAsync(Component());
        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");

        Scheduler.Reset();
        using var pump = TestSchedulerPump.Install();
        var container = TestServerMarkup.Parse(html);
        var renderer = new TestRenderer();
        var root = VirtualNodeFactory.Component(Component());
        renderer.Hydrate(root, container);

        // The fragment adopts the [ ] anchor comments with no structural mutation.
        var log = renderer.OperationLog;
        log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        log.Count(TestNodeOperationType.CreateComment).ShouldBe(0);
        log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        var subtree = ((ComponentInstance)root.Component!).Subtree!;
        ((TestComment)subtree.El!).Text.ShouldBe("[");
        ((TestComment)subtree.Anchor!).Text.ShouldBe("]");
        Scheduler.Reset();
    }
}
