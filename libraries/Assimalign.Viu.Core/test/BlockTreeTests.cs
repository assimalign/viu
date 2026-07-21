using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the block-tree fast paths against @vue/runtime-core's openBlock/setupBlock/patchBlockChildren
// and the patchElement patch-flag matrix (packages/runtime-core/src/vnode.ts + renderer.ts) —
// https://vuejs.org/guide/extras/rendering-mechanism.html#compiler-informed-virtual-dom. Skipping a
// static subtree is skipping marshaled node-op calls on WASM, so the O(K) visit claim is pinned with
// both the node-op log and the internal patch-visit counter ([V01.01.03.15]).
public class BlockTreeTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public BlockTreeTests()
    {
        Scheduler.Reset();
        BlockStack.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;
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
    public void OpenBlock_CollectsDynamicChildren_FlattenedAcrossStaticDepth()
    {
        // Upstream setupBlock: a block's dynamicChildren are the dynamic descendants created within
        // it, flattened past static wrapper elements — the static <div> is not collected, but the
        // dynamic <span> nested inside it is.
        VirtualNodeFactory.OpenBlock();
        var block = VirtualNodeFactory.ElementBlock(
            "section",
            null,
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element(
                    "div",
                    null,
                    new VirtualNode?[] { VirtualNodeFactory.Element("span", null, "msg", PatchFlags.Text) }),
            });

        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren.Count.ShouldBe(1);
        block.DynamicChildren[0].ElementTag.ShouldBe("span");
    }

    [Fact]
    public void PatchBlock_VisitsOnlyDynamicNodes_SkippingTheStaticSubtree()
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
        Renderer<TestNode>.PatchVisitCount = 0;

        _renderer.Render(Build("b"), _container);

        // O(K): only the block root and the single dynamic <span> are visited; the three static
        // children are never passed to patch. K = 1 dynamic binding, not N = 3 static nodes.
        Renderer<TestNode>.PatchVisitCount.ShouldBe(2);
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><div><header>static-1</header><nav>static-2</nav><footer>static-3</footer><span>b</span></div></root>");
    }

    [Fact]
    public void PatchBlock_TrustsTheCompiler_LeavingAStaticChildUntouchedEvenIfItsContentDiffers()
    {
        // A block skips static children entirely (upstream parity): the compiler promised the <p> is
        // static, so a differing value in the re-render's vnode is never applied — proof the static
        // subtree is not walked.
        VirtualNode Build(string staticText, string dynamicText)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                null,
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("p", staticText),
                    VirtualNodeFactory.Element("span", null, dynamicText, PatchFlags.Text),
                });
        }

        _renderer.Render(Build("first", "a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Build("second", "b"), _container);

        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><p>first</p><span>b</span></div></root>");
    }

    [Fact]
    public void NestedBlock_PatchesTheInnerBlockThroughTheOuter()
    {
        VirtualNode Inner(string message)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                null,
                new VirtualNode?[] { VirtualNodeFactory.Element("span", null, message, PatchFlags.Text) });
        }

        VirtualNode Build(string message)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "section",
                null,
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("h1", "static-title"),
                    Inner(message),
                });
        }

        _renderer.Render(Build("a"), _container);
        _renderer.OperationLog.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;

        _renderer.Render(Build("b"), _container);

        // The outer block collects the inner block (a block is always a dynamic child of its parent);
        // the visit chain is section -> inner div -> span, and the static <h1> is skipped.
        Renderer<TestNode>.PatchVisitCount.ShouldBe(3);
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe(
            "<root><section><h1>static-title</h1><div><span>b</span></div></section></root>");
    }

    [Fact]
    public void StableFragmentBlock_PatchesDynamicChildrenPositionally_WithoutTheKeyedDiff()
    {
        VirtualNode Build(string first, string second)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.FragmentBlock(
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("span", null, first, PatchFlags.Text),
                    VirtualNodeFactory.Element("span", null, second, PatchFlags.Text),
                },
                key: null,
                PatchFlags.StableFragment);
        }

        _renderer.Render(Build("a1", "b1"), _container);
        _renderer.OperationLog.Reset();
        Renderer<TestNode>.PatchVisitCount = 0;

        _renderer.Render(Build("a2", "b2"), _container);

        // A stable fragment with a block tree patches its dynamic children positionally (upstream
        // processFragment): two targeted set-text, no structural moves and no keyed reconciliation.
        Renderer<TestNode>.PatchVisitCount.ShouldBe(3);
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(2);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><span>a2</span><span>b2</span></root>");
    }

    [Fact]
    public void Bail_ProducesIdenticalResultToTheOptimizedBlock()
    {
        // PatchFlags.Bail exits optimized mode into a full diff; the same scenario patched optimized
        // versus bailed must produce identical serialized DOM (upstream parity).
        var bailedRenderer = new TestRenderer();
        var bailedContainer = bailedRenderer.CreateContainer();

        VirtualNode Optimized(string message)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                VirtualNodeFactory.Properties(("id", "root")),
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("p", "static"),
                    VirtualNodeFactory.Element("span", null, message, PatchFlags.Text),
                });
        }

        VirtualNode Bailed(string message) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", "root")),
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("p", "static"),
                VirtualNodeFactory.Element("span", message),
            },
            PatchFlags.Bail);

        _renderer.Render(Optimized("a"), _container);
        bailedRenderer.Render(Bailed("a"), bailedContainer);
        _renderer.Render(Optimized("b"), _container);
        bailedRenderer.Render(Bailed("b"), bailedContainer);

        const string expected = "<root><div id=\"root\"><p>static</p><span>b</span></div></root>";
        TestNodeSerializer.Serialize(_container).ShouldBe(expected);
        TestNodeSerializer.Serialize(bailedContainer).ShouldBe(expected);
    }

    [Fact]
    public void OpenBlock_WithDisableTracking_DoesNotCollectDynamicChildren()
    {
        // Upstream openBlock(true): v-once content — a dynamic child created inside is not collected,
        // so the block carries an empty dynamicChildren and the content re-renders only via the full
        // walk (never the block fast path).
        VirtualNodeFactory.OpenBlock(disableTracking: true);
        var block = VirtualNodeFactory.ElementBlock(
            "div",
            null,
            new VirtualNode?[] { VirtualNodeFactory.Element("span", null, "once", PatchFlags.Text) });

        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren.ShouldBeEmpty();
    }

    [Fact]
    public void SetBlockTracking_SuspendsThenResumesCollection()
    {
        // Upstream setBlockTracking: a v-once expression brackets its cached content with -1 / +1 so
        // it is not collected, while content created after tracking resumes is.
        VirtualNodeFactory.OpenBlock();
        VirtualNodeFactory.SetBlockTracking(-1);
        var cached = VirtualNodeFactory.Element("span", null, "cached", PatchFlags.Text);
        VirtualNodeFactory.SetBlockTracking(1);
        var live = VirtualNodeFactory.Element("em", null, "live", PatchFlags.Text);
        var block = VirtualNodeFactory.ElementBlock("div", null, new VirtualNode?[] { cached, live });

        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren.Count.ShouldBe(1);
        block.DynamicChildren[0].ElementTag.ShouldBe("em");
    }

    [Fact]
    public void ComponentChild_IsAlwaysCollected_EvenWithoutAPatchFlag()
    {
        // Upstream createBaseVNode: a component is collected regardless of patch flag, because its
        // instance must persist to the next vnode even when its own props do not change.
        var component = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Element("i", "child"),
        };
        VirtualNodeFactory.OpenBlock();
        var block = VirtualNodeFactory.ElementBlock(
            "div",
            null,
            new VirtualNode?[]
            {
                VirtualNodeFactory.Element("hr"),
                VirtualNodeFactory.Component(component),
            });

        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren.Count.ShouldBe(1);
        block.DynamicChildren[0].Type.ShouldBe(VirtualNodeType.Component);
    }

    [Fact]
    public void PatchBlock_WithAComponentChild_UpdatesTheComponentThroughTheBlock()
    {
        var childRenders = 0;
        var child = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("label")],
            SetupFunction = (properties, _) => () =>
            {
                childRenders++;
                return VirtualNodeFactory.Element("i", (string?)properties["label"] ?? "none");
            },
        };

        VirtualNode Build(string label)
        {
            VirtualNodeFactory.OpenBlock();
            return VirtualNodeFactory.ElementBlock(
                "div",
                null,
                new VirtualNode?[]
                {
                    VirtualNodeFactory.Element("hr"),
                    VirtualNodeFactory.Component(child, VirtualNodeFactory.Properties(("label", label)), PatchFlags.Props, ["label"]),
                });
        }

        _renderer.Render(Build("one"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><hr></hr><i>one</i></div></root>");
        childRenders.ShouldBe(1);

        _renderer.Render(Build("two"), _container);

        // The block patches the component (resolving its real container via the host parent) and
        // never the static <hr>; the child re-renders once with the changed prop.
        childRenders.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><hr></hr><i>two</i></div></root>");
    }

    [Fact]
    public void CompiledStyleFlag_PatchesOnlyTheStyleProperty()
    {
        // Upstream patchElement STYLE fast path: only the style prop is visited (the stable id is not).
        VirtualNode Compiled(string style) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("style", style), ("id", "stable")),
            (VirtualNode?[]?)null,
            PatchFlags.Style);

        _renderer.Render(Compiled("color:red"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Compiled("color:blue"), _container);

        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(1);
        patches[0].PropertyName.ShouldBe("style");
        patches[0].NextValue.ShouldBe("color:blue");
    }

    [Fact]
    public void CompiledFullPropsFlag_FullyDiffsThePropertyBag()
    {
        // Upstream patchElement FULL_PROPS fast path: a full prop-bag diff patches only the changed
        // key (id), skipping the unchanged one (title).
        VirtualNode Compiled(string id, string title) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", id), ("title", title)),
            (VirtualNode?[]?)null,
            PatchFlags.FullProps);

        _renderer.Render(Compiled("a", "stable-title"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Compiled("b", "stable-title"), _container);

        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(1);
        patches[0].PropertyName.ShouldBe("id");
        patches[0].NextValue.ShouldBe("b");
    }

    [Fact]
    public void RenderFunctionThrowingMidBlock_DoesNotCorruptLaterBlockCollection()
    {
        // Upstream renderComponentRoot clears the block stack in its catch (blockStack.length = 0).
        // A render that throws between OpenBlock and its closing block factory, with the error
        // swallowed by the app-level errorHandler, must not leak its open accumulator into later
        // renders' dynamic-child collection.
        var failing = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                VirtualNodeFactory.OpenBlock();
                throw new InvalidOperationException("mid-block boom");
            },
        };
        var application = _renderer.Renderer.CreateApplication(failing);
        var handled = 0;
        application.Config.ErrorHandler = (_, _, _) => handled++;
        application.Mount(_container);
        handled.ShouldBe(1);

        // A later, unrelated block render must collect exactly its own dynamic children.
        VirtualNodeFactory.OpenBlock();
        var block = VirtualNodeFactory.ElementBlock(
            "div",
            null,
            [
                VirtualNodeFactory.Element("span", null, "static", default),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("id", "x")), (VirtualNode?[]?)null, PatchFlags.Props, ["id"]),
            ]);

        block.DynamicChildren.ShouldNotBeNull();
        block.DynamicChildren!.Count.ShouldBe(1);
        block.DynamicChildren[0].ElementTag.ShouldBe("span");
    }
}
