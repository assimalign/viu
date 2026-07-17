using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the component instance + Setup contract of @vue/runtime-core's component.ts —
// https://vuejs.org/api/composition-api-setup.html. Run counts asserted throughout: Setup runs
// once per instance; the returned render function re-executes per update.
public class ComponentInstanceTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ComponentInstanceTests()
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
    public void Component_MountsThroughTheRenderer_AndSetupRunsExactlyOnce()
    {
        var setupRuns = 0;
        var renderRuns = 0;
        var message = Reactive.Reference("first");
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                setupRuns++;
                return () =>
                {
                    renderRuns++;
                    return VirtualNodeFactory.Element("div", message.Value);
                };
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>first</div></root>");
        setupRuns.ShouldBe(1);
        renderRuns.ShouldBe(1);

        message.Value = "second";
        _pump.RunUntilIdle();

        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>second</div></root>");
        setupRuns.ShouldBe(1); // never re-runs
        renderRuns.ShouldBe(2); // the render function re-executes per update
    }

    [Fact]
    public void CurrentInstance_IsCorrectDuringNestedSetups_AndNullOutside()
    {
        ComponentInstance? parentDuringSetup = null;
        ComponentInstance? childDuringSetup = null;
        ComponentInstance? parentAfterChildMount = null;

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                childDuringSetup = ComponentInstance.Current;
                return static () => VirtualNodeFactory.Element("span", "child");
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                parentDuringSetup = ComponentInstance.Current;
                return () =>
                {
                    var tree = VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(child));
                    parentAfterChildMount = ComponentInstance.Current;
                    return tree;
                };
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        parentDuringSetup.ShouldNotBeNull();
        childDuringSetup.ShouldNotBeNull();
        childDuringSetup.ShouldNotBeSameAs(parentDuringSetup);
        childDuringSetup.Parent.ShouldBeSameAs(parentDuringSetup);
        childDuringSetup.Root.ShouldBeSameAs(parentDuringSetup);
        parentAfterChildMount.ShouldBeSameAs(parentDuringSetup); // stack restored around child
        ComponentInstance.Current.ShouldBeNull(); // null outside setup/lifecycle
    }

    [Fact]
    public void CurrentInstance_IsRestoredWhenSetupThrows()
    {
        var throwing = new TestComponent
        {
            SetupFunction = (_, _) => throw new InvalidOperationException("setup boom"),
        };

        Should.Throw<InvalidOperationException>(
            () => _renderer.Render(VirtualNodeFactory.Component(throwing), _container));

        ComponentInstance.Current.ShouldBeNull();
    }

    [Fact]
    public void Uids_AreCreationOrdered_ParentBeforeChild()
    {
        ComponentInstance? parentInstance = null;
        ComponentInstance? childInstance = null;
        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                childInstance = ComponentInstance.Current;
                return static () => VirtualNodeFactory.Text("child");
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                parentInstance = ComponentInstance.Current;
                return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(child));
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        parentInstance!.Uid.ShouldBeLessThan(childInstance!.Uid);
    }

    [Fact]
    public void Expose_RestrictsWhatTheInstanceSurfaces()
    {
        var exposed = new Dictionary<string, object?> { ["focus"] = (Action)(() => { }) };
        ComponentInstance? instance = null;
        var component = new TestComponent
        {
            SetupFunction = (_, context) =>
            {
                context.Expose(exposed);
                instance = ComponentInstance.Current;
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        instance!.Exposed.ShouldBeSameAs(exposed);
    }

    [Fact]
    public void InstanceState_TracksMountAndUnmount()
    {
        ComponentInstance? instance = null;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                instance = ComponentInstance.Current;
                return static () => VirtualNodeFactory.Element("div", "content");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        instance!.IsMounted.ShouldBeTrue();
        instance.IsUnmounted.ShouldBeFalse();
        instance.Subtree.ShouldNotBeNull();
        instance.VirtualNode.El.ShouldNotBeNull(); // vnode el points at the subtree root

        _renderer.Render(null, _container);
        instance.IsUnmounted.ShouldBeTrue();
        _container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void UnmountingAComponent_StopsItsSetupEffects()
    {
        var source = Reactive.Reference(0);
        var effectRuns = 0;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Reactive.Effect(() =>
                {
                    _ = source.Value;
                    effectRuns++;
                });
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        effectRuns.ShouldBe(1);

        _renderer.Render(null, _container);
        source.Value = 42;
        _pump.RunUntilIdle();

        effectRuns.ShouldBe(1); // the instance scope stopped the setup effect
    }

    [Fact]
    public void ParentDrivenUpdate_ReusesTheInstance_AndSkipsWhenPropsUnchanged()
    {
        var setupRuns = 0;
        var childRenderRuns = 0;
        var child = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("label")],
            SetupFunction = (properties, _) =>
            {
                setupRuns++;
                return () =>
                {
                    childRenderRuns++;
                    return VirtualNodeFactory.Element("span", (string?)properties["label"] ?? "none");
                };
            },
        };
        var parentMessage = Reactive.Reference("a");
        var childLabel = Reactive.Reference("one");
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text(parentMessage.Value),
                VirtualNodeFactory.Component(child, VirtualNodeFactory.Properties(("label", childLabel.Value)))),
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>a<span>one</span></div></root>");
        childRenderRuns.ShouldBe(1);

        // Parent re-renders with an UNCHANGED child prop: the child must not re-render.
        parentMessage.Value = "b";
        _pump.RunUntilIdle();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>b<span>one</span></div></root>");
        setupRuns.ShouldBe(1);
        childRenderRuns.ShouldBe(1);

        // Parent re-renders with a CHANGED child prop: the child re-renders once.
        childLabel.Value = "two";
        _pump.RunUntilIdle();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>b<span>two</span></div></root>");
        setupRuns.ShouldBe(1);
        childRenderRuns.ShouldBe(2);
    }

    [Fact]
    public void ComponentRoots_CanBeFragments()
    {
        var component = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Fragment(
                VirtualNodeFactory.Element("span", "a"),
                VirtualNodeFactory.Element("span", "b")),
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><span>a</span><span>b</span></root>");

        _renderer.Render(null, _container);
        _container.Children.ShouldBeEmpty(); // fragment range + anchors fully removed
    }

    [Fact]
    public void SiblingReplacement_KeepsComponentPosition()
    {
        var componentA = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Element("span", "A"),
        };
        var componentB = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Element("span", "B"),
        };

        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text("start"),
                VirtualNodeFactory.Component(componentA),
                VirtualNodeFactory.Text("end")),
            _container);
        _renderer.Render(
            VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text("start"),
                VirtualNodeFactory.Component(componentB),
                VirtualNodeFactory.Text("end")),
            _container);

        // A different component definition replaces in place, between its siblings.
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>start<span>B</span>end</div></root>");
    }
}
