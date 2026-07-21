using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the hydration walker against @vue/runtime-core's createHydrationFunctions
// (packages/runtime-core/src/hydration.ts) — https://vuejs.org/guide/scaling-up/ssr.html#client-hydration.
// The walker adopts the server-rendered TestNode tree (built with TestServerMarkup, the DOM-free stand-in
// for parsing SSR output) instead of creating nodes: a clean hydration performs zero structural node
// operations, event handlers become live, PatchFlag fast paths skip static work, and a server/client
// mismatch recovers per subtree without crashing ([V01.01.07.03]).
public class HydrationTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestNodeOperationLog _log;
    private readonly TestSchedulerPump _pump;

    public HydrationTests()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        _pump = TestSchedulerPump.Install();
        _log = _renderer.OperationLog;
    }

    public void Dispose()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        _pump.Dispose();
    }

    // --- adoption without mutation --------------------------------------------------------------

    [Fact]
    public void HydrateElement_MatchingServerMarkup_AdoptsWithoutStructuralMutation()
    {
        var container = TestServerMarkup.Parse("<div>hello</div>");
        var serverDiv = container.Children[0];
        var vnode = VirtualNodeFactory.Element("div", "hello");

        _renderer.Hydrate(vnode, container);

        AssertAdoptedWithoutMutation();
        // The vnode adopts the existing server node rather than creating a new one.
        vnode.El.ShouldBeSameAs(serverDiv);
    }

    [Fact]
    public void HydrateElement_AttachesEventListener_WithoutTouchingTheDom()
    {
        var clicks = 0;
        var container = TestServerMarkup.Parse("<button>Click</button>");
        var vnode = VirtualNodeFactory.Element(
            "button",
            VirtualNodeFactory.Properties(("onClick", (Action)(() => clicks++))),
            "Click",
            PatchFlags.NeedHydration);

        _renderer.Hydrate(vnode, container);

        // Zero DOM mutations, yet the listener is live (upstream: event listeners are always attached).
        AssertAdoptedWithoutMutation();
        var button = (TestElement)container.Children[0];
        button.EventListeners.ShouldContainKey("click");
        ((Action)button.EventListeners["click"])();
        clicks.ShouldBe(1);
    }

    [Fact]
    public void HydrateElement_CachedSubtree_AdoptedWithoutInspectingChildren()
    {
        // A CACHED (hoisted / v-once) element is trusted verbatim — its children are never walked
        // (upstream hydrateElement: the `patchFlag !== CACHED` gate). Even a server/client child
        // difference is left untouched because the fast path never inspects it.
        var container = TestServerMarkup.Parse("<div><span>server</span></div>");
        var vnode = VirtualNodeFactory.Element(
            "div",
            null,
            new VirtualNode?[] { VirtualNodeFactory.Element("span", "client") },
            PatchFlags.Cached);

        _renderer.Hydrate(vnode, container);

        AssertAdoptedWithoutMutation();
        // The child span vnode was never descended into, so it holds no adopted node.
        vnode.ArrayChildren![0].El.ShouldBeNull();
    }

    [Fact]
    public void HydrateElement_ReconcilesOnlyDynamicProps_LeavingStaticPropsUntouched()
    {
        // The PatchFlag fast path: only the compiler's dynamicProps are reconciled during the walk; the
        // static id attribute is never patched (upstream: the ShouldHydrateProperty qualification).
        var container = TestServerMarkup.Parse("<div id=\"static\" title=\"live\">hi</div>");
        var vnode = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", "static"), ("title", "live")),
            "hi",
            PatchFlags.Props,
            new[] { "title" });

        _renderer.Hydrate(vnode, container);

        _log.Count(TestNodeOperationType.PatchProperty).ShouldBe(1);
        _log.OfType(TestNodeOperationType.PatchProperty)[0].PropertyName.ShouldBe("title");
        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
    }

    [Fact]
    public void HydrateFragment_MatchingAnchors_AdoptsChildrenBetweenBrackets()
    {
        var container = TestServerMarkup.Parse("<!--[--><span>a</span><span>b</span><!--]-->");
        var vnode = VirtualNodeFactory.Fragment(
            new VirtualNode?[] { VirtualNodeFactory.Element("span", "a"), VirtualNodeFactory.Element("span", "b") },
            null,
            PatchFlags.StableFragment);

        _renderer.Hydrate(vnode, container);

        AssertAdoptedWithoutMutation();
        // El/Anchor are the [ and ] comment anchors the server emitted.
        ((TestComment)vnode.El!).Text.ShouldBe("[");
        ((TestComment)vnode.Anchor!).Text.ShouldBe("]");
        vnode.ArrayChildren![0].El.ShouldBeSameAs(container.Children[1]);
    }

    [Fact]
    public void HydrateComment_MatchingComment_Adopted()
    {
        var container = TestServerMarkup.Parse("<!--anchor-->");
        var vnode = VirtualNodeFactory.Comment("anchor");

        _renderer.Hydrate(vnode, container);

        AssertAdoptedWithoutMutation();
        vnode.El.ShouldBeSameAs(container.Children[0]);
    }

    // --- mismatch detection + recovery ----------------------------------------------------------

    [Fact]
    public void Hydrate_TextContentMismatch_WarnsAndCorrectsInPlace()
    {
        var container = TestServerMarkup.Parse("<div>server</div>");
        var vnode = VirtualNodeFactory.Element("div", "client");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("text content mismatch", StringComparison.OrdinalIgnoreCase));
        // Recovery corrects the text to the client value, without a remount (Insert stays zero).
        _log.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        ((TestElement)container.Children[0]).Children.Count.ShouldBe(1);
        ((TestText)((TestElement)container.Children[0]).Children[0]).Text.ShouldBe("client");
    }

    [Fact]
    public void Hydrate_StructuralMismatch_RecoversSubtree_WhileAdoptingTheRest()
    {
        // The outer <div> matches and is adopted; only the mismatched <span>-vs-<p> child is torn down and
        // client-rendered (upstream handleMismatch). A mismatch never crashes and the tree converges.
        var container = TestServerMarkup.Parse("<div><span>x</span></div>");
        var serverDiv = container.Children[0];
        var vnode = VirtualNodeFactory.Element(
            "div", null, new VirtualNode?[] { VirtualNodeFactory.Element("p", "x") });
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        // The outer div was adopted, not recreated.
        vnode.El.ShouldBeSameAs(serverDiv);
        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(1); // just the replacement <p>
        _log.Count(TestNodeOperationType.Remove).ShouldBe(1);        // the stale <span>
        // Final DOM converges to the client tree.
        var div = (TestElement)container.Children[0];
        div.Children.Count.ShouldBe(1);
        ((TestElement)div.Children[0]).Tag.ShouldBe("p");
    }

    [Fact]
    public void Hydrate_DataAllowMismatch_SuppressesTheWarning_ButStillRecovers()
    {
        var container = TestServerMarkup.Parse("<div data-allow-mismatch=\"children\"><span>x</span></div>");
        var vnode = VirtualNodeFactory.Element(
            "div", null, new VirtualNode?[] { VirtualNodeFactory.Element("p", "x") });
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        // The escape hatch suppresses the warning...
        warnings.Messages.ShouldBeEmpty();
        // ...but the tree still converges to the client vdom.
        _log.Count(TestNodeOperationType.Remove).ShouldBe(1);
        ((TestElement)((TestElement)container.Children[0]).Children[0]).Tag.ShouldBe("p");
    }

    [Fact]
    public void Hydrate_ClassMismatch_Warns()
    {
        var container = TestServerMarkup.Parse("<div class=\"a b\">hi</div>");
        var vnode = VirtualNodeFactory.Element(
            "div", VirtualNodeFactory.Properties(("class", "a c")), "hi");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("class mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Hydrate_ClassMismatch_SuppressedByDataAllowMismatch()
    {
        var container = TestServerMarkup.Parse("<div class=\"a b\" data-allow-mismatch=\"class\">hi</div>");
        var vnode = VirtualNodeFactory.Element(
            "div", VirtualNodeFactory.Properties(("class", "a c")), "hi");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void HydrateElement_ExcessServerChildren_RemovedWithWarning()
    {
        var container = TestServerMarkup.Parse("<ul><li>a</li><li>b</li></ul>");
        var vnode = VirtualNodeFactory.Element("ul", null, new VirtualNode?[] { VirtualNodeFactory.Element("li", "a") });
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("more child nodes", StringComparison.OrdinalIgnoreCase));
        _log.Count(TestNodeOperationType.Remove).ShouldBe(1);
        ((TestElement)container.Children[0]).Children.Count.ShouldBe(1);
    }

    // --- components + reactive updates ----------------------------------------------------------

    [Fact]
    public void HydrateComponent_AdoptsSubtree_AndReactiveUpdatePatchesInPlace_NoRemount()
    {
        var message = Reactive.Reference("hello");
        var component = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element("div", null, message.Value, PatchFlags.Text),
        };
        var container = TestServerMarkup.Parse("<div>hello</div>");
        var serverDiv = container.Children[0];
        var root = VirtualNodeFactory.Component(component);

        _renderer.Hydrate(root, container);

        // Clean hydration: nothing created, inserted, or removed.
        AssertAdoptedWithoutMutation();

        // A reactive change patches the adopted node in place — no remount (Insert/CreateElement stay 0).
        _log.Reset();
        message.Value = "world";
        _pump.RunUntilIdle();

        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _log.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        // The same server element carries the new text.
        container.Children[0].ShouldBeSameAs(serverDiv);
        ((TestText)((TestElement)serverDiv).Children[0]).Text.ShouldBe("world");
    }

    [Fact]
    public void HydrateComponent_MultiRootFragment_AdoptsViaAnchorComments()
    {
        var component = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Fragment(
                new VirtualNode?[] { VirtualNodeFactory.Element("span", "a"), VirtualNodeFactory.Element("span", "b") },
                null,
                PatchFlags.StableFragment),
        };
        var container = TestServerMarkup.Parse("<!--[--><span>a</span><span>b</span><!--]-->");
        var root = VirtualNodeFactory.Component(component);

        _renderer.Hydrate(root, container);

        AssertAdoptedWithoutMutation();
        var instance = (ComponentInstance)root.Component!;
        ((TestComment)instance.Subtree!.El!).Text.ShouldBe("[");
        ((TestComment)instance.Subtree!.Anchor!).Text.ShouldBe("]");
    }

    // --- teleport -------------------------------------------------------------------------------

    [Fact]
    public void HydrateTeleport_Enabled_AdoptsContentAtTarget()
    {
        var target = TestServerMarkup.Parse("<div id=\"modal\"><span>x</span><!--teleport anchor--></div>");
        _renderer.RegisterQueryRoot(target);
        var modal = (TestElement)target.Children[0];
        var targetSpan = modal.Children[0];

        var container = TestServerMarkup.Parse("<!--teleport start--><!--teleport end-->");
        var teleport = VirtualNodeFactory.Teleport(
            VirtualNodeFactory.Properties(("to", "#modal")),
            new VirtualNode?[] { VirtualNodeFactory.Element("span", "x") });

        _renderer.Hydrate(teleport, container);

        AssertAdoptedWithoutMutation();
        // The teleported child adopted the node already sitting in the target, not a fresh one.
        teleport.ArrayChildren![0].El.ShouldBeSameAs(targetSpan);
        // The main-tree footprint is the start anchor.
        ((TestComment)teleport.El!).Text.ShouldBe("teleport start");
    }

    // --- root fallbacks -------------------------------------------------------------------------

    [Fact]
    public void Hydrate_EmptyContainer_FallsBackToFullMount()
    {
        var container = _renderer.CreateContainer();
        var vnode = VirtualNodeFactory.Element("div", "fresh");
        using var warnings = new WarningCapture();

        _renderer.Hydrate(vnode, container);

        warnings.Messages.ShouldContain(message => message.Contains("container is empty", StringComparison.OrdinalIgnoreCase));
        // A full mount creates and inserts the tree.
        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(1);
        _log.Count(TestNodeOperationType.Insert).ShouldBe(1);
        container.Children.Count.ShouldBe(1);
        ((TestElement)container.Children[0]).Tag.ShouldBe("div");
    }

    private void AssertAdoptedWithoutMutation()
    {
        // A clean hydration issues no node-creating or structural operations — every node is adopted from
        // the server DOM (upstream: zero DOM mutations). Property patches (listener attachment) are allowed.
        _log.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        _log.Count(TestNodeOperationType.CreateText).ShouldBe(0);
        _log.Count(TestNodeOperationType.CreateComment).ShouldBe(0);
        _log.Count(TestNodeOperationType.Insert).ShouldBe(0);
        _log.Count(TestNodeOperationType.Remove).ShouldBe(0);
        _log.Count(TestNodeOperationType.SetElementText).ShouldBe(0);
        _log.Count(TestNodeOperationType.SetText).ShouldBe(0);
        _log.Count(TestNodeOperationType.InsertStaticContent).ShouldBe(0);
    }
}
