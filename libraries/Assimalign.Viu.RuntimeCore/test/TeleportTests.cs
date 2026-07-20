using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the platform-agnostic Teleport built-in against the in-memory renderer, DOM-free — mirroring
// upstream runtime-core/__tests__/components/Teleport.spec.ts. Teleport is a special vnode type
// (ShapeFlags.Teleport) handled in the patch/move/unmount paths, not an ordinary component; the
// contract is packages/runtime-core/src/components/Teleport.ts and
// https://vuejs.org/guide/built-ins/teleport.html. [V01.01.03.17]
public sealed class TeleportTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public TeleportTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    private string Serialize() => TestNodeSerializer.Serialize(_container);

    private static VirtualNode ElementWithId(string tag, string id)
        => VirtualNodeFactory.Element(tag, VirtualNodeFactory.Properties(("id", id)), (VirtualNode?[]?)null);

    // --- target resolution -----------------------------------------------------------------------

    [Fact]
    public void Mount_ResolvesStringTargetThroughQuerySelector_MountsChildrenIntoTarget_AndLeavesAnchorsInPlace()
    {
        // Target-first ordering: a non-deferred Teleport must resolve its target at mount (upstream: the
        // target element must exist before the teleport is mounted). #modal resolves through the test
        // adapter's querySelector node-op (the in-memory stand-in for the DOM querySelector).
        var tree = VirtualNodeFactory.Fragment(
            ElementWithId("div", "modal"),
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", "#modal")),
                [VirtualNodeFactory.Element("span", "teleported")]));
        _renderer.Render(tree, _container);

        // The teleported <span> lives inside div#modal; the Teleport leaves only its (empty) anchor pair
        // at its own tree position.
        Serialize().ShouldBe("<root><div id=\"modal\"><span>teleported</span></div></root>");
    }

    [Fact]
    public void Mount_ResolvesDirectTargetNode_MountsChildrenIntoIt()
    {
        var target = _renderer.CreateContainer("target");
        _renderer.Render(
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", target)),
                [VirtualNodeFactory.Element("span", "direct")]),
            _container);

        // A direct platform-node `to` is used as-is (no querySelector); the main container keeps only the
        // anchor pair (empty text, invisible).
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>direct</span></target>");
        Serialize().ShouldBe("<root></root>");
    }

    [Fact]
    public void Mount_StringTargetThatDoesNotExist_Warns_AndDoesNotTeleport()
    {
        using var warnings = new WarningCapture();
        _renderer.Render(
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", "#missing")),
                [VirtualNodeFactory.Element("span", "orphan")]),
            _container);

        // Upstream resolveTarget warns when a selector target is absent and the teleport is enabled; the
        // children are not mounted anywhere.
        warnings.Messages.ShouldContain(message => message.Contains("Failed to locate Teleport target"));
        Serialize().ShouldBe("<root></root>");
    }

    // --- disabled --------------------------------------------------------------------------------

    [Fact]
    public void Mount_Disabled_RendersChildrenInPlace_NotInTarget()
    {
        var target = _renderer.CreateContainer("target");
        _renderer.Render(
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", target), ("disabled", true)),
                [VirtualNodeFactory.Element("span", "inplace")]),
            _container);

        // disabled: children render between the main-tree anchors; the target only gets the (empty)
        // framing anchors.
        Serialize().ShouldBe("<root><span>inplace</span></root>");
        TestNodeSerializer.Serialize(target).ShouldBe("<target></target>");
    }

    [Fact]
    public void ToggleDisabled_MovesChildrenBetweenTargetAndInPlace_WithoutUnmounting()
    {
        var target = _renderer.CreateContainer("target");
        var mountCount = 0;
        var unmountCount = 0;
        var child = new TestComponent
        {
            Name = "Child",
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnMounted(() => mountCount++);
                Lifecycle.OnUnmounted(() => unmountCount++);
                return () => VirtualNodeFactory.Element("span", "child");
            },
        };
        VirtualNode Build(bool disabled) => VirtualNodeFactory.Teleport(
            VirtualNodeFactory.Properties(("to", target), ("disabled", disabled)),
            [VirtualNodeFactory.Component(child)]);

        // Enabled: the child mounts into the target.
        _renderer.Render(Build(disabled: false), _container);
        mountCount.ShouldBe(1);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>child</span></target>");
        Serialize().ShouldBe("<root></root>");

        // enabled -> disabled: the child moves in place; the SAME instance is reused (no remount) — the
        // acceptance criterion that subtree state is preserved, pinned by the mount/unmount run counts.
        _renderer.Render(Build(disabled: true), _container);
        mountCount.ShouldBe(1);
        unmountCount.ShouldBe(0);
        Serialize().ShouldBe("<root><span>child</span></root>");
        TestNodeSerializer.Serialize(target).ShouldBe("<target></target>");

        // disabled -> enabled: the child moves back into the target, still the same instance.
        _renderer.Render(Build(disabled: false), _container);
        mountCount.ShouldBe(1);
        unmountCount.ShouldBe(0);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>child</span></target>");
        Serialize().ShouldBe("<root></root>");
    }

    [Fact]
    public void ReactiveDisabledToggle_MovesTeleportedContent_ThroughTheScheduler()
    {
        var target = _renderer.CreateContainer("target");
        var disabled = Reactive.Reference(false);
        _renderer.Renderer.CreateRenderEffect(
            () => VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", target), ("disabled", disabled.Value)),
                [VirtualNodeFactory.Element("span", "x")]),
            _container);

        // Enabled: teleported into the target.
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>x</span></target>");
        Serialize().ShouldBe("<root></root>");

        // A reactive flip of `disabled` re-renders through the scheduler and moves the content in place.
        disabled.Value = true;
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><span>x</span></root>");
        TestNodeSerializer.Serialize(target).ShouldBe("<target></target>");
    }

    // --- target change ---------------------------------------------------------------------------

    [Fact]
    public void ChangeTarget_MovesChildrenToNewTarget()
    {
        var first = _renderer.CreateContainer("first");
        var second = _renderer.CreateContainer("second");
        VirtualNode Build(TestElement target) => VirtualNodeFactory.Teleport(
            VirtualNodeFactory.Properties(("to", target)),
            [VirtualNodeFactory.Element("span", "moving")]);

        _renderer.Render(Build(first), _container);
        TestNodeSerializer.Serialize(first).ShouldBe("<first><span>moving</span></first>");

        // Changing `to` moves the children into the new target (upstream: moveTeleport TARGET_CHANGE).
        _renderer.Render(Build(second), _container);
        TestNodeSerializer.Serialize(second).ShouldBe("<second><span>moving</span></second>");
        TestNodeSerializer.Serialize(first).ShouldBe("<first></first>");
    }

    [Fact]
    public void MultipleTeleports_ToSameTarget_AppendInMountOrder_AndPatchIndependently()
    {
        var target = _renderer.CreateContainer("target");
        VirtualNode Build(string first, string second) => VirtualNodeFactory.Fragment(
            VirtualNodeFactory.Teleport(VirtualNodeFactory.Properties(("to", target)), [VirtualNodeFactory.Element("span", first)]),
            VirtualNodeFactory.Teleport(VirtualNodeFactory.Properties(("to", target)), [VirtualNodeFactory.Element("span", second)]));

        _renderer.Render(Build("A", "B"), _container);
        // Two Teleports to the same target append their content in mount order.
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>A</span><span>B</span></target>");

        // They patch independently: only the second Teleport's child changes.
        _renderer.Render(Build("A", "B2"), _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>A</span><span>B2</span></target>");
    }

    [Fact]
    public void KeyedReorderOfTeleports_MovesTheMainAnchors_ButLeavesEnabledContentInTarget()
    {
        var target = _renderer.CreateContainer("target");
        VirtualNode Keyed(string key, string text) => VirtualNodeFactory.Teleport(
            VirtualNodeFactory.Properties(("key", key), ("to", target)),
            [VirtualNodeFactory.Element("span", text)]);
        VirtualNode Build(string firstKey, string secondKey) => VirtualNodeFactory.Fragment(
            [Keyed(firstKey, firstKey), Keyed(secondKey, secondKey)],
            key: "list",
            PatchFlags.KeyedFragment);

        _renderer.Render(Build("a", "b"), _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>a</span><span>b</span></target>");

        // Reordering the two keyed Teleports in the main tree moves only their (empty) main anchors; the
        // enabled content stays put in the target in mount order (upstream: moveTeleport REORDER does not
        // move an enabled Teleport's children).
        _renderer.Render(Build("b", "a"), _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>a</span><span>b</span></target>");
    }

    // --- defer -----------------------------------------------------------------------------------

    [Fact]
    public void Deferred_ResolvesTargetRenderedLaterInTheSameTree_AfterMount()
    {
        // The target div is rendered AFTER the Teleport in the same tree, so it does not exist when the
        // Teleport mounts. `defer` resolves the target in the post-flush phase, once the whole tree has
        // mounted (upstream 3.5 queuePendingMount).
        var tree = VirtualNodeFactory.Fragment(
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", "#late"), ("defer", true)),
                [VirtualNodeFactory.Element("span", "deferred")]),
            ElementWithId("div", "late"));
        _renderer.Render(tree, _container);

        // After the synchronous render drains its post-flush queue, the span is inside the later div#late.
        Serialize().ShouldBe("<root><div id=\"late\"><span>deferred</span></div></root>");
    }

    [Fact]
    public void NonDeferred_TargetRenderedLater_FailsToResolveAtMount_AndWarns()
    {
        using var warnings = new WarningCapture();
        var tree = VirtualNodeFactory.Fragment(
            VirtualNodeFactory.Teleport(
                VirtualNodeFactory.Properties(("to", "#late")),
                [VirtualNodeFactory.Element("span", "orphan")]),
            ElementWithId("div", "late"));
        _renderer.Render(tree, _container);

        // Without defer the target does not yet exist at mount, so the children are not teleported and the
        // upstream dev warning fires — the contrast that pins what defer actually changes.
        warnings.Messages.ShouldContain(message => message.Contains("Failed to locate Teleport target"));
        Serialize().ShouldBe("<root><div id=\"late\"></div></root>");
    }

    // --- unmount ---------------------------------------------------------------------------------

    [Fact]
    public void Unmount_RemovesChildrenFromTarget_AndRunsUnmountLifecycles()
    {
        var target = _renderer.CreateContainer("target");
        var unmounted = 0;
        var child = new TestComponent
        {
            Name = "Child",
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnUnmounted(() => unmounted++);
                return () => VirtualNodeFactory.Element("span", "child");
            },
        };
        _renderer.Render(
            VirtualNodeFactory.Teleport(VirtualNodeFactory.Properties(("to", target)), [VirtualNodeFactory.Component(child)]),
            _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>child</span></target>");

        // Unmounting the Teleport removes its children from the target and runs their unmount lifecycles.
        _renderer.Render(null, _container);
        unmounted.ShouldBe(1);
        TestNodeSerializer.Serialize(target).ShouldBe("<target></target>");
        Serialize().ShouldBe("<root></root>");
    }

    // --- compiled-render dispatch + block form ---------------------------------------------------

    [Fact]
    public void RenderHelpers_Teleport_DispatchesToTeleportVNode_AndRenders()
    {
        var target = _renderer.CreateContainer("target");
        // The shape the template compiler emits for <Teleport>: the _Teleport marker as the vnode tag,
        // children as a vnode array (never slots).
        var vnode = RenderHelpers._createVNode(
            RenderHelpers._Teleport,
            RenderHelpers._createProps(("to", target)),
            new object?[] { RenderHelpers._createElementVNode("span", null, "compiled") });

        vnode.Type.ShouldBe(VirtualNodeType.Teleport);
        _renderer.Render(vnode, _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><span>compiled</span></target>");
    }

    [Fact]
    public void BlockTeleport_PatchesOnlyDynamicChild_AndPreservesStaticChild()
    {
        var target = _renderer.CreateContainer("target");
        // A compiled block Teleport: a static <div> child (patchFlag 0, skipped on update) alongside a
        // dynamic-text <span> child (PatchFlags.Text, collected into dynamicChildren).
        VirtualNode Build(string text)
        {
            var block = RenderHelpers._openBlock();
            return RenderHelpers._createBlock(
                block,
                RenderHelpers._Teleport,
                RenderHelpers._createProps(("to", target)),
                new object?[]
                {
                    RenderHelpers._createElementVNode("div", null, "static"),
                    RenderHelpers._createElementVNode("span", null, text, (int)PatchFlags.Text),
                });
        }

        _renderer.Render(Build("one"), _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><div>static</div><span>one</span></target>");

        // The block update patches only the dynamic <span> through the target container; the static
        // <div> keeps its host node (traverseStaticChildren) and its content.
        _renderer.Render(Build("two"), _container);
        TestNodeSerializer.Serialize(target).ShouldBe("<target><div>static</div><span>two</span></target>");
    }
}
