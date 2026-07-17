using System;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Shared;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins slots against @vue/runtime-core's componentSlots.ts and helpers/renderSlot.ts —
// https://vuejs.org/guide/components/slots.html. Run counts pin the SlotFlags stability contract
// (a stable slot must not force a child re-render on a parent-only update — an interop saver).
public class SlotsTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public SlotsTests()
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

    [Fact]
    public void RenderSlot_RendersProvidedContent_AndFallbackWhenAbsent()
    {
        var slots = new ComponentSlots
        {
            ["default"] = _ => [VirtualNodeFactory.Element("span", "provided")],
        };
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.RenderSlot(context.Slots, "default", null, () => [VirtualNodeFactory.Element("em", "fb-default")]),
                VirtualNodeFactory.RenderSlot(context.Slots, "header", null, () => [VirtualNodeFactory.Element("h1", "fb-header")])),
        };

        _renderer.Render(VirtualNodeFactory.Component(child, null, slots), _container);

        // Default slot provided → its content; header slot absent → its fallback.
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><div><span>provided</span><h1>fb-header</h1></div></root>");
    }

    [Fact]
    public void ScopedSlot_ReceivesChildProvidedProps()
    {
        object? received = null;
        var slots = new ComponentSlots
        {
            ["default"] = properties =>
            {
                received = properties;
                return [VirtualNodeFactory.Element("span", (string?)properties ?? "none")];
            },
        };
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.RenderSlot(context.Slots, "default", "child-scope")),
        };

        _renderer.Render(VirtualNodeFactory.Component(child, null, slots), _container);

        received.ShouldBe("child-scope");
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>child-scope</span></div></root>");
    }

    [Fact]
    public void ScopedSlot_ReRendersWithUpdatedChildScope_OnChildUpdate()
    {
        var count = Reactive.Reference(0);
        var slots = new ComponentSlots
        {
            ["default"] = properties => [VirtualNodeFactory.Element("span", $"count:{properties}")],
        };
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.RenderSlot(context.Slots, "default", count.Value)),
        };

        _renderer.Render(VirtualNodeFactory.Component(child, null, slots), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>count:0</span></div></root>");

        // The child re-renders on its own state change and passes the new scope to the slot.
        count.Value = 5;
        _pump.RunUntilIdle();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>count:5</span></div></root>");
    }

    [Fact]
    public void StableSlots_DoNotForceChildUpdate_OnParentOnlyReRender()
    {
        var childRenderRuns = 0;
        var parentState = Reactive.Reference("a");
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () =>
            {
                childRenderRuns++;
                return VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default"));
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                var slots = new ComponentSlots(SlotFlags.Stable)
                {
                    ["default"] = _ => [VirtualNodeFactory.Element("span", "slot")],
                };
                return VirtualNodeFactory.Element(
                    "section",
                    VirtualNodeFactory.Text(parentState.Value),
                    VirtualNodeFactory.Component(child, null, slots));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        childRenderRuns.ShouldBe(1);

        // Parent re-renders its own text; stable slots + unchanged props must not force the child.
        parentState.Value = "b";
        _pump.RunUntilIdle();
        childRenderRuns.ShouldBe(1);
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><section>b<div><span>slot</span></div></section></root>");
    }

    [Fact]
    public void DynamicSlots_ForceChildUpdate_OnParentReRender()
    {
        var childRenderRuns = 0;
        var parentState = Reactive.Reference("a");
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () =>
            {
                childRenderRuns++;
                return VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default"));
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                var slots = new ComponentSlots(SlotFlags.Dynamic)
                {
                    ["default"] = _ => [VirtualNodeFactory.Element("span", "slot")],
                };
                return VirtualNodeFactory.Element(
                    "section",
                    VirtualNodeFactory.Text(parentState.Value),
                    VirtualNodeFactory.Component(child, null, slots));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        childRenderRuns.ShouldBe(1);

        parentState.Value = "b";
        _pump.RunUntilIdle();
        childRenderRuns.ShouldBe(2); // dynamic slots force the child to re-render
    }

    [Fact]
    public void DynamicSlotsPatchFlag_ForcesChildUpdate()
    {
        var childRenderRuns = 0;
        var parentState = Reactive.Reference("a");
        var child = new TestComponent
        {
            SetupFunction = (_, context) => () =>
            {
                childRenderRuns++;
                return VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default"));
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                // Slots object is Stable, but the compiled DYNAMIC_SLOTS patch flag forces the update.
                var slots = new ComponentSlots(SlotFlags.Stable)
                {
                    ["default"] = _ => [VirtualNodeFactory.Element("span", "slot")],
                };
                return VirtualNodeFactory.Element(
                    "section",
                    VirtualNodeFactory.Text(parentState.Value),
                    VirtualNodeFactory.Component(child, null, slots, PatchFlags.DynamicSlots));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        childRenderRuns.ShouldBe(1);

        parentState.Value = "b";
        _pump.RunUntilIdle();
        childRenderRuns.ShouldBe(2);
    }

    [Fact]
    public void ForwardedSlots_FromStableParent_DoNotForceGrandchildUpdate()
        => RunForwardingScenario(SlotFlags.Stable).ShouldBe(1);

    [Fact]
    public void ForwardedSlots_FromDynamicParent_ForceGrandchildUpdate()
        => RunForwardingScenario(SlotFlags.Dynamic).ShouldBe(2);

    [Fact]
    public void SlotReactiveReads_AttributeToTheInvokingChild_NotTheDefiningParent()
    {
        // Upstream withCtx parity: a slot's reactive reads are tracked by the child effect that
        // invokes the slot, not the parent effect that defined it. The parent never reads the dep,
        // so a change re-renders only the child.
        var slotDependency = Reactive.Reference("x");
        var parentRenderRuns = 0;
        var childRenderRuns = 0;

        var child = new TestComponent
        {
            SetupFunction = (_, context) => () =>
            {
                childRenderRuns++;
                return VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default"));
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                parentRenderRuns++;
                var slots = new ComponentSlots(SlotFlags.Stable)
                {
                    // Reads the dep only when INVOKED (in the child), never during parent render.
                    ["default"] = _ => [VirtualNodeFactory.Element("span", slotDependency.Value)],
                };
                return VirtualNodeFactory.Component(child, null, slots);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        parentRenderRuns.ShouldBe(1);
        childRenderRuns.ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>x</span></div></root>");

        slotDependency.Value = "y";
        _pump.RunUntilIdle();

        childRenderRuns.ShouldBe(2);
        parentRenderRuns.ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>y</span></div></root>");
    }

    [Fact]
    public void RenderSlot_InvokedOutsideAnyRenderEffect_ReturnsContentSafely()
    {
        var dependency = Reactive.Reference("v");
        var slots = new ComponentSlots
        {
            ["default"] = _ => [VirtualNodeFactory.Element("span", dependency.Value)],
        };

        // No active render effect: invoking the slot must neither throw nor strand tracking.
        var fragment = VirtualNodeFactory.RenderSlot(slots, "default");

        fragment.Type.ShouldBe(VirtualNodeType.Fragment);
        fragment.ArrayChildren!.Length.ShouldBe(1);
        Should.NotThrow(() =>
        {
            dependency.Value = "changed";
        });
    }

    // Parent -> Middle (forwards its slots) -> Grandchild. The forwarded slots' stability is
    // resolved from the flag Parent passed to Middle, so it propagates the forced-update contract.
    private int RunForwardingScenario(SlotFlags parentToMiddleFlag)
    {
        var grandchildRenderRuns = 0;
        var parentState = Reactive.Reference("a");

        var grandchild = new TestComponent
        {
            Name = "Grandchild",
            SetupFunction = (_, context) => () =>
            {
                grandchildRenderRuns++;
                return VirtualNodeFactory.Element("div", VirtualNodeFactory.RenderSlot(context.Slots, "default"));
            },
        };
        var middle = new TestComponent
        {
            Name = "Middle",
            SetupFunction = (_, context) => () =>
            {
                var forwarded = new ComponentSlots(SlotFlags.Forwarded)
                {
                    ["default"] = properties => context.Slots?["default"]?.Invoke(properties),
                };
                return VirtualNodeFactory.Component(grandchild, null, forwarded);
            },
        };
        var parent = new TestComponent
        {
            Name = "Parent",
            SetupFunction = (_, _) => () =>
            {
                var slots = new ComponentSlots(parentToMiddleFlag)
                {
                    ["default"] = _ => [VirtualNodeFactory.Element("span", "leaf")],
                };
                return VirtualNodeFactory.Element(
                    "section",
                    VirtualNodeFactory.Text(parentState.Value),
                    VirtualNodeFactory.Component(middle, null, slots));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        grandchildRenderRuns.ShouldBe(1);
        TestNodeSerializer.Serialize(_container)
            .ShouldBe("<root><section>a<div><span>leaf</span></div></section></root>");

        parentState.Value = "b";
        _pump.RunUntilIdle();
        return grandchildRenderRuns;
    }
}
