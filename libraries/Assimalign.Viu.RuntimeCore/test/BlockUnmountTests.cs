using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the block unmount fast path against @vue/runtime-core's unmount / unmountChildren
// (packages/runtime-core/src/renderer.ts): a block vnode tears down only the dynamic descendants it
// collected — static content leaves with the host subtree removal — while a v-once block (#5154) and a
// non-stable fragment (#1153) fall back to the full children walk. Skipping a static teardown visit is
// skipping marshaled node-op work on WASM, so the O(K) claim is pinned with the internal unmount-visit
// counter, and the differential tests prove buffered content is torn down identically to the full walk
// (lifecycle hooks fire, refs null, directives' beforeUnmount/unmounted fire) ([V01.01.03.15.01]).
public class BlockUnmountTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public BlockUnmountTests()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;
        Renderer<TestNode>.UnmountVisitCount = 0;
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void UnmountBlock_VisitsOnlyDynamicNodes_SkippingTheStaticSubtree()
    {
        VirtualNode Build(string message)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                null,
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("header", "static-1"),
                    VirtualNodeFactory.Element("nav", "static-2"),
                    VirtualNodeFactory.Element("footer", "static-3"),
                    VirtualNodeFactory.Element("span", null, message, PatchFlags.Text),
                });
        }

        _renderer.Render(Build("a"), _container);
        _renderer.OperationLog.Reset();
        Renderer<TestNode>.UnmountVisitCount = 0;

        _renderer.Render(null, _container);

        // O(K): only the block root and the single dynamic <span> are visited on teardown; the three
        // static children are never passed to unmount. Their platform nodes leave in the block's one
        // host removal (upstream unmount fast path for block nodes).
        Renderer<TestNode>.UnmountVisitCount.ShouldBe(2);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void UnmountBlock_DoesNotReWalkTheStaticChildrenOfADynamicElement()
    {
        VirtualNode Build(string cls)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                null,
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element(
                        "section",
                        VirtualNodeFactory.Properties(("class", cls)),
                        new VirtualNode?[]
                        {
                            VirtualNodeFactory.Element("span", "a"),
                            VirtualNodeFactory.Element("span", "b"),
                        },
                        PatchFlags.Class),
                });
        }

        _renderer.Render(Build("red"), _container);
        Renderer<TestNode>.UnmountVisitCount = 0;

        _renderer.Render(null, _container);

        // The dynamic <section> is torn down, but its two static <span> grandchildren are not: upstream
        // threads `optimized` through the fast-path recursion so a collected element does not re-walk
        // its own static children. Without that threading the count would be 4, not 2.
        Renderer<TestNode>.UnmountVisitCount.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void UnmountStableFragmentBlock_TearsDownDynamicChildrenOnly_SkippingStaticSiblings()
    {
        VirtualNode Build(string message)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.FragmentBlock(
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("hr", "static"),
                    VirtualNodeFactory.Element("span", null, message, PatchFlags.Text),
                },
                key: null,
                PatchFlags.StableFragment);
        }

        _renderer.Render(Build("a"), _container);
        Renderer<TestNode>.UnmountVisitCount = 0;

        _renderer.Render(null, _container);

        // A stable fragment carrying a block tree takes the fast path (upstream: type !== Fragment ||
        // STABLE_FRAGMENT): only the fragment block and its one dynamic <span> are visited; the static
        // <hr> is not. The fragment's whole owned host range still leaves in one anchored removal walk.
        Renderer<TestNode>.UnmountVisitCount.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void UnmountBlock_FiresLifecycleRefsAndDirectives_ForDynamicDescendants()
    {
        var events = new List<string>();
        var elementRef = new Reference<object?>(null);
        var directive = new Directive
        {
            BeforeUnmount = (_, _, _, _) => events.Add("dir:beforeUnmount"),
            Unmounted = (_, _, _, _) => events.Add("dir:unmounted"),
        };
        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnUnmounted(() => events.Add("child:unmounted"));
                return static () => VirtualNodeFactory.Element("i", "child");
            },
        };
        var host = new TestComponent
        {
            // The block is produced inside a render (WithDirectives needs the active instance); its
            // dynamic descendants are the directive-bearing <span> (NeedPatch), the ref, and the child
            // component. The static <header> is not collected.
            SetupFunction = (_, _) => () =>
            {
                VirtualNodeFactory.OpenBlock();
                return VirtualNodeFactory.ElementBlock(
                    "div",
                    null,
                    new VirtualNode?[]
                    {
                        VirtualNodeFactory.Element("header", "static"),
                        Directives.WithDirectives(
                            VirtualNodeFactory.Element(
                                "span",
                                VirtualNodeFactory.Properties(("ref", elementRef)),
                                "x",
                                PatchFlags.NeedPatch),
                            directive),
                        VirtualNodeFactory.Component(child),
                    });
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(host), _container);
        elementRef.Value.ShouldNotBeNull();

        _renderer.Render(null, _container);

        // The fast path visits only the dynamic descendants, but tears each down completely — a
        // component's unmounted hook, a directive's beforeUnmount and unmounted, and a template ref all
        // fire exactly as the full walk would (upstream unmount parity).
        events.ShouldContain("dir:beforeUnmount");
        events.ShouldContain("dir:unmounted");
        events.ShouldContain("child:unmounted");
        elementRef.Value.ShouldBeNull();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void UnmountVOnceBlock_TakesTheFullWalk_TearingDownAComponentNestedInVOnceContent()
    {
        // Upstream #5154: a component created under setBlockTracking(-1, true) is absent from the
        // block's DynamicChildren, so the block must NOT take the unmount fast path — the fast path
        // would skip (leak) the component. The v-once mark (HasOnce) forces the full children walk that
        // reaches and tears it down.
        var events = new List<string>();
        var onceChild = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnUnmounted(() => events.Add("once:unmounted"));
                return static () => VirtualNodeFactory.Element("i", "once");
            },
        };

        VirtualNodeFactory.OpenBlock();
        var liveSpan = VirtualNodeFactory.Element("span", null, "live", PatchFlags.Text); // collected
        VirtualNodeFactory.SetBlockTracking(-1, inVOnce: true);
        var onceComponent = VirtualNodeFactory.Component(onceChild); // NOT collected; marks HasOnce
        VirtualNodeFactory.SetBlockTracking(1);
        var block = VirtualNodeFactory.ElementBlock("div", null, new VirtualNode?[] { liveSpan, onceComponent });

        block.HasOnce.ShouldBeTrue();                       // the block carries the v-once mark
        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren!.Count.ShouldBe(1);           // only the live <span>, not the component

        _renderer.Render(block, _container);
        _renderer.Render(null, _container);

        // Full walk (fast path skipped) reaches the uncollected component and tears it down.
        events.ShouldBe(["once:unmounted"]);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void SetBlockTracking_MarksHasOnceOnlyForAVOnceSuspension_NotAPlainOne()
    {
        // Upstream setBlockTracking(value, inVOnce): only a v-once suspension marks the block (#5154);
        // a plain -1/+1 bracket suspends collection without setting hasOnce.
        VirtualNodeFactory.OpenBlock();
        VirtualNodeFactory.SetBlockTracking(-1);
        VirtualNodeFactory.SetBlockTracking(1);
        var plain = VirtualNodeFactory.ElementBlock("div", null, (VirtualNode?[]?)null);
        plain.HasOnce.ShouldBeFalse();

        VirtualNodeFactory.OpenBlock();
        VirtualNodeFactory.SetBlockTracking(-1, inVOnce: true);
        VirtualNodeFactory.SetBlockTracking(1);
        var once = VirtualNodeFactory.ElementBlock("div", null, (VirtualNode?[]?)null);
        once.HasOnce.ShouldBeTrue();
    }

    [Fact]
    public void UnmountBlock_RemovesTheHostSubtree_IdenticallyToABailedFullWalk()
    {
        // A block (optimized teardown) and the equivalent bailed tree (full-walk teardown) must leave
        // the container in the same state — the fast path changes which vnodes are visited, never the
        // host result (upstream parity).
        var bailedRenderer = new TestRenderer();
        var bailedContainer = bailedRenderer.CreateContainer();

        VirtualNode Optimized()
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                VirtualNodeFactory.Properties(("id", "root")),
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("p", "static"),
                    VirtualNodeFactory.Element("span", null, "a", PatchFlags.Text),
                });
        }

        VirtualNode Bailed() => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", "root")),
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("p", "static"),
                VirtualNodeFactory.Element("span", "a"),
            },
            PatchFlags.Bail);

        _renderer.Render(Optimized(), _container);
        bailedRenderer.Render(Bailed(), bailedContainer);
        _renderer.Render(null, _container);
        bailedRenderer.Render(null, bailedContainer);

        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
        TestNodeSerializer.Serialize(bailedContainer).ShouldBe("<root></root>");
    }
}
