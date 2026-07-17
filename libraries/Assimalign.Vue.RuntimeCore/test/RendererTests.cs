using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Shared;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the mount/patch/unmount pipeline contract of @vue/runtime-core's renderer.ts
// (createRenderer) — https://vuejs.org/api/custom-renderer.html — through the in-memory test
// tree, asserting both final output and node-op counts (the CoreCLR proxy for interop cost).
public class RendererTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public RendererTests()
    {
        Scheduler.Reset();
        // The pump captures scheduled flushes — without it the scheduler's thread-pool
        // fallback would race these single-threaded assertions.
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Render_MountsAnElementTree_WithExpectedOps()
    {
        var tree = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", "app")),
            VirtualNodeFactory.Element("span", "hello"));

        _renderer.Render(tree, _container);

        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div id=\"app\"><span>hello</span></div></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(2);
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.Insert).ShouldBe(2);
        _renderer.OperationLog.Count(TestNodeOperationType.PatchProperty).ShouldBe(1);
    }

    [Fact]
    public void Render_PatchesOnSubsequentCalls_AndUnmountsOnNull()
    {
        _renderer.Render(VirtualNodeFactory.Element("div", "a"), _container);
        _renderer.Render(VirtualNodeFactory.Element("div", "b"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>b</div></root>");

        _renderer.Render(null, _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
        _container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Patch_TextOnlyChange_ProducesExactlyOneSetElementTextAndNoStructuralOps()
    {
        _renderer.Render(VirtualNodeFactory.Element("div", "a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(VirtualNodeFactory.Element("div", "b"), _container);

        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
    }

    [Fact]
    public void Patch_MismatchedElementTags_UnmountAndRemountInPlace()
    {
        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Element("span", "first"),
                VirtualNodeFactory.Element("b", "middle"),
                VirtualNodeFactory.Element("span", "last")),
            _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Element("span", "first"),
                VirtualNodeFactory.Element("i", "middle"),
                VirtualNodeFactory.Element("span", "last")),
            _container);

        // The replacement lands in the middle position, not appended at the end.
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div><span>first</span><i>middle</i><span>last</span></div></root>");
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(1);
    }

    [Fact]
    public void Patch_DifferentKeysAtTheSamePosition_Replace()
    {
        // Same-type check includes the key (upstream isSameVNodeType).
        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("key", "a")), "content"),
            _container);
        var firstMounted = _container.Children[0];
        _renderer.OperationLog.Reset();

        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("key", "b")), "content"),
            _container);

        _container.Children[0].ShouldNotBeSameAs(firstMounted);
        _renderer.OperationLog.Count(TestNodeOperationType.Remove).ShouldBe(1);
    }

    [Fact]
    public void Fragment_MountsBetweenStartAndEndAnchors()
    {
        _renderer.Render(
            VirtualNodeFactory.Fragment(
                VirtualNodeFactory.Element("span", "a"),
                VirtualNodeFactory.Element("span", "b")),
            _container);

        // Fragment anchors are empty text nodes (upstream parity): [start, a, b, end].
        _container.Children.Count.ShouldBe(4);
        _container.Children[0].ShouldBeOfType<TestText>().Text.ShouldBe(string.Empty);
        _container.Children[3].ShouldBeOfType<TestText>().Text.ShouldBe(string.Empty);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><span>a</span><span>b</span></root>");
    }

    [Fact]
    public void Fragment_AppendedChildren_InsertBeforeTheEndAnchor()
    {
        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Fragment(VirtualNodeFactory.Element("span", "a")),
                VirtualNodeFactory.Element("footer", "after")),
            _container);

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Fragment(
                    VirtualNodeFactory.Element("span", "a"),
                    VirtualNodeFactory.Element("span", "b")),
                VirtualNodeFactory.Element("footer", "after")),
            _container);

        // The grown fragment child lands inside the fragment range, before the footer.
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div><span>a</span><span>b</span><footer>after</footer></div></root>");
    }

    [Fact]
    public void Fragment_Unmount_RemovesExactlyItsOwnedRange()
    {
        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text("before"),
                VirtualNodeFactory.Fragment(
                    VirtualNodeFactory.Element("span", "a"),
                    VirtualNodeFactory.Element("span", "b")),
                VirtualNodeFactory.Text("after")),
            _container);

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text("before"),
                VirtualNodeFactory.Text("after")),
            _container);

        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>beforeafter</div></root>");
        var divElement = (TestElement)_container.Children[0];
        divElement.Children.Count.ShouldBe(2); // no anchor leftovers
    }

    [Fact]
    public void Unmount_InvokesCleanupBeforeNodeRemoval()
    {
        var wasAttachedDuringHook = false;
        var tree = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(
                ("onVnodeBeforeUnmount", (VirtualNodeHook)((node, _) =>
                {
                    // Cleanup order: the platform node is still attached when the hook runs.
                    wasAttachedDuringHook = ((TestNode)node.El!).Parent is not null;
                }))),
            "content");

        _renderer.Render(tree, _container);
        _renderer.Render(null, _container);

        wasAttachedDuringHook.ShouldBeTrue();
        _container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void VnodeHooks_FireInPipelineOrder()
    {
        var events = new List<string>();
        VirtualNodeProperties HookProperties() => VirtualNodeFactory.Properties(
            ("onVnodeBeforeMount", (VirtualNodeHook)((_, _) => events.Add("beforeMount"))),
            ("onVnodeMounted", (VirtualNodeHook)((_, _) => events.Add("mounted"))),
            ("onVnodeBeforeUpdate", (VirtualNodeHook)((_, _) => events.Add("beforeUpdate"))),
            ("onVnodeUpdated", (VirtualNodeHook)((_, _) => events.Add("updated"))),
            ("onVnodeBeforeUnmount", (VirtualNodeHook)((_, _) => events.Add("beforeUnmount"))),
            ("onVnodeUnmounted", (VirtualNodeHook)((_, _) => events.Add("unmounted"))));

        _renderer.Render(VirtualNodeFactory.Element("div", HookProperties(), "a"), _container);
        events.ShouldBe(["beforeMount", "mounted"]);

        events.Clear();
        _renderer.Render(VirtualNodeFactory.Element("div", HookProperties(), "b"), _container);
        events.ShouldBe(["beforeUpdate", "updated"]);

        events.Clear();
        _renderer.Render(null, _container);
        events.ShouldBe(["beforeUnmount", "unmounted"]);
    }

    [Fact]
    public void TextRoot_PatchesWithASingleSetTextOp()
    {
        _renderer.Render(VirtualNodeFactory.Text("a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(VirtualNodeFactory.Text("b"), _container);

        _renderer.OperationLog.Count(TestNodeOperationType.SetText).ShouldBe(1);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root>b</root>");
    }

    [Fact]
    public void Comment_ContentIsNeverPatched()
    {
        // Upstream parity: processCommentNode reuses the node and ignores content changes.
        _renderer.Render(VirtualNodeFactory.Comment("one"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(VirtualNodeFactory.Comment("two"), _container);

        _renderer.OperationLog.Operations.ShouldBeEmpty();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><!--one--></root>");
    }

    [Fact]
    public void CompiledTextFlag_TakesTheTargetedPath()
    {
        VirtualNode Compiled(string text) => VirtualNodeFactory.Element(
            "div", null, text, PatchFlags.Text);

        _renderer.Render(Compiled("a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Compiled("b"), _container);

        // The compiled contract: one targeted set-text, nothing else (each avoided op is an
        // avoided interop call on WASM).
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.Operations.Count.ShouldBe(1);
    }

    [Fact]
    public void CompiledClassFlag_PatchesOnlyWhenTheClassChanged()
    {
        VirtualNode Compiled(string cssClass) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", cssClass), ("id", "stable")),
            (VirtualNode?[]?)null,
            PatchFlags.Class);

        _renderer.Render(Compiled("a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Compiled("a"), _container);
        _renderer.OperationLog.Count(TestNodeOperationType.PatchProperty).ShouldBe(0);

        _renderer.Render(Compiled("b"), _container);
        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(1);
        patches[0].PropertyName.ShouldBe("class");
    }

    [Fact]
    public void CompiledPropsFlag_PatchesOnlyTheDeclaredDynamicProperties()
    {
        VirtualNode Compiled(string title) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("title", title), ("id", "stable")),
            (VirtualNode?[]?)null,
            PatchFlags.Props,
            ["title"]);

        _renderer.Render(Compiled("a"), _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(Compiled("b"), _container);

        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(1);
        patches[0].PropertyName.ShouldBe("title");
        patches[0].NextValue.ShouldBe("b");
    }

    [Fact]
    public void FullDiff_RemovesStaleAndPatchesChangedProperties()
    {
        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("a", "1"), ("b", "2")), "x"),
            _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("a", "changed")), "x"),
            _container);

        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(2);
        patches[0].PropertyName.ShouldBe("b");
        patches[0].NextValue.ShouldBeNull(); // stale prop removed
        patches[1].PropertyName.ShouldBe("a");
        patches[1].NextValue.ShouldBe("changed");
    }

    [Fact]
    public void ValueProperty_IsAlwaysRepatched()
    {
        // Upstream parity: the live platform value can drift from the vnode value (typing, IME),
        // so "value" is forced through patchProp even when the vnode value is unchanged.
        _renderer.Render(
            VirtualNodeFactory.Element("input", VirtualNodeFactory.Properties(("value", "same"))),
            _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(
            VirtualNodeFactory.Element("input", VirtualNodeFactory.Properties(("value", "same"))),
            _container);

        var patches = _renderer.OperationLog.OfType(TestNodeOperationType.PatchProperty);
        patches.Count.ShouldBe(1);
        patches[0].PropertyName.ShouldBe("value");
    }

    [Fact]
    public void UnkeyedChildren_GrowAndShrinkPositionally()
    {
        VirtualNode List(params string[] items)
        {
            var children = new VirtualNode?[items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                children[index] = VirtualNodeFactory.Element("li", items[index]);
            }
            return VirtualNodeFactory.Element("ul", null, children);
        }

        _renderer.Render(List("a", "b"), _container);
        _renderer.Render(List("a", "b", "c"), _container);
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><ul><li>a</li><li>b</li><li>c</li></ul></root>");

        _renderer.Render(List("a"), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><ul><li>a</li></ul></root>");
    }

    [Fact]
    public void StaticVnode_MountsThroughInsertStaticContent_InOneOperation()
    {
        _renderer.Render(
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Static("<b>chunk</b><i>tail</i>")),
            _container);

        _renderer.OperationLog.Count(TestNodeOperationType.InsertStaticContent).ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><b>chunk</b><i>tail</i></div></root>");
    }

    [Fact]
    public void StaticVnode_WithoutTheOp_FailsWithAClearContractError()
    {
        var log = new TestNodeOperationLog();
        var complete = TestNodeOperations.Create(log);
        var withoutStatic = new RendererOptions<TestNode>
        {
            Insert = complete.Insert,
            Remove = complete.Remove,
            CreateElement = complete.CreateElement,
            CreateText = complete.CreateText,
            CreateComment = complete.CreateComment,
            SetText = complete.SetText,
            SetElementText = complete.SetElementText,
            ParentNode = complete.ParentNode,
            NextSibling = complete.NextSibling,
            PatchProperty = complete.PatchProperty,
        };
        var renderer = RendererFactory.CreateRenderer(withoutStatic);

        var exception = Should.Throw<NotSupportedException>(
            () => renderer.Render(VirtualNodeFactory.Static("<b>x</b>"), _renderer.CreateContainer()));
        exception.Message.ShouldContain("InsertStaticContent");
    }

    [Fact]
    public void SvgSubtree_SwitchesNamespace_AndForeignObjectChildrenReturnToHtml()
    {
        // Upstream namespace propagation: <svg> subtree is SVG; <foreignObject> children go back
        // to HTML (renderer.ts mountElement + resolveChildrenNamespace).
        _renderer.Render(
            VirtualNodeFactory.Element(
                "svg",
                VirtualNodeFactory.Element(
                    "foreignObject",
                    VirtualNodeFactory.Element("div", "html-island")),
                VirtualNodeFactory.Element("circle")),
            _container);

        var svg = (TestElement)_container.Children[0];
        svg.Namespace.ShouldBe("svg");
        var foreignObject = (TestElement)svg.Children[0];
        foreignObject.Namespace.ShouldBe("svg");
        ((TestElement)foreignObject.Children[0]).Namespace.ShouldBeNull();
        ((TestElement)svg.Children[1]).Namespace.ShouldBe("svg");
    }

    [Fact]
    public void MathSubtree_SwitchesToMathmlNamespace()
    {
        _renderer.Render(
            VirtualNodeFactory.Element("math", VirtualNodeFactory.Element("mi", "x")),
            _container);

        var math = (TestElement)_container.Children[0];
        math.Namespace.ShouldBe("mathml");
        ((TestElement)math.Children[0]).Namespace.ShouldBe("mathml");
    }

    [Fact]
    public void RenderingTheSameVnodeInstance_IsANoOp()
    {
        var tree = VirtualNodeFactory.Element("div", "a");
        _renderer.Render(tree, _container);
        _renderer.OperationLog.Reset();

        _renderer.Render(tree, _container);

        _renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void ReusingAMountedVnode_ClonesInsteadOfCorruptingTheOriginal()
    {
        // Upstream cloneIfMounted contract: an already-mounted vnode reused in another tree is
        // cloned at mount, so the original keeps its own el.
        var shared = VirtualNodeFactory.Element("span", "shared");
        _renderer.Render(VirtualNodeFactory.Element("div", shared), _container);
        var originalElement = shared.El;
        originalElement.ShouldNotBeNull();

        var secondContainer = _renderer.CreateContainer();
        _renderer.Render(VirtualNodeFactory.Element("div", shared), secondContainer);

        shared.El.ShouldBeSameAs(originalElement); // original untouched
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>shared</span></div></root>");
        TestNodeSerializer.Serialize(secondContainer).ShouldBe("<root><div><span>shared</span></div></root>");
    }

}
