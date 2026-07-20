using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu.RuntimeCore;

using static Assimalign.Viu.RuntimeCore.VirtualNodeFactory;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Exercises Teleport ([V01.01.03.17]) over the browser-shaped int-handle node-ops (RendererOptions<int>
// wired to the InMemoryHandleDom exactly as production BrowserNodeOperations wires the JS bridge), so the
// value-type TNode path — boxing handles into the teleport state, the default(TNode)==0 "no node"
// sentinel — is covered as well as the reference-type TestNode path is in RuntimeCore. Browser-annotated
// like the other tests touching the browser-only node-ops types; nothing crosses a real interop boundary.
[SupportedOSPlatform("browser")]
public sealed class TeleportDomTests
{
    [Fact]
    public void Teleport_MovesChildrenIntoDirectTargetHandle_ThroughIntHandleNodeOps()
    {
        Scheduler.Reset();
        var dom = new InMemoryHandleDom();
        using var world = new DirectHandleDomWorld(dom);
        var renderer = RendererFactory.CreateRenderer(world.Options);
        var container = dom.CreateElement("root", null);
        var target = dom.CreateElement("target", null);

        renderer.Render(Teleport(Properties(("to", target)), [Element("span", "boxed")]), container);

        // The teleported <span> lands in the direct target handle; the main container keeps only the
        // (empty) anchor pair.
        dom.Serialize(target).ShouldBe("<target><span>boxed</span></target>");
        dom.Serialize(container).ShouldBe("<root></root>");
    }

    [Fact]
    public void Teleport_StringTargetResolvingToTheZeroHandle_IsTreatedAsNotFound_AndDoesNotThrow()
    {
        Scheduler.Reset();
        var dom = new InMemoryHandleDom();
        // DirectHandleDomWorld stubs querySelector to always return 0 — the "no node" sentinel for int
        // handles. Without treating 0 as not-found the renderer would try to insert anchors into handle 0
        // and throw; the sentinel guard in resolveTarget keeps the browser adapter safe.
        using var world = new DirectHandleDomWorld(dom);
        var renderer = RendererFactory.CreateRenderer(world.Options);
        var container = dom.CreateElement("root", null);

        Should.NotThrow(() => renderer.Render(Teleport(Properties(("to", "#anywhere")), [Element("span", "x")]), container));

        // Nothing is teleported (no valid target); the container holds only the anchor pair.
        dom.Serialize(container).ShouldBe("<root></root>");
    }
}
