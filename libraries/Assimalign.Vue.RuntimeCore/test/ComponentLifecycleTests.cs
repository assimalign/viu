using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the Composition API lifecycle contract of @vue/runtime-core's apiLifecycle.ts and
// errorHandling.ts — https://vuejs.org/api/composition-api-lifecycle.html. Ordering matrices
// asserted as event lists.
public class ComponentLifecycleTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;
    private readonly List<string> _events = [];

    public ComponentLifecycleTests()
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

    private TestComponent HookedComponent(string name, Func<VirtualNode?> render, TestComponent? childToRender = null)
        => new()
        {
            Name = name,
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnBeforeMount(() => _events.Add($"{name}:beforeMount"));
                Lifecycle.OnMounted(() => _events.Add($"{name}:mounted"));
                Lifecycle.OnBeforeUpdate(() => _events.Add($"{name}:beforeUpdate"));
                Lifecycle.OnUpdated(() => _events.Add($"{name}:updated"));
                Lifecycle.OnBeforeUnmount(() => _events.Add($"{name}:beforeUnmount"));
                Lifecycle.OnUnmounted(() => _events.Add($"{name}:unmounted"));
                return childToRender is null
                    ? render
                    : () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(childToRender));
            },
        };

    [Fact]
    public void MountOrdering_BeforeMountParentFirst_MountedChildFirst()
    {
        var child = HookedComponent("child", static () => VirtualNodeFactory.Element("span", "c"));
        var parent = HookedComponent("parent", static () => null, child);

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        _events.ShouldBe(
        [
            "parent:beforeMount",
            "child:beforeMount",
            "child:mounted",   // post-flush, child before parent (stable queue order)
            "parent:mounted",
        ]);
    }

    [Fact]
    public void UnmountOrdering_BeforeUnmountParentFirst_UnmountedChildFirst()
    {
        var child = HookedComponent("child", static () => VirtualNodeFactory.Element("span", "c"));
        var parent = HookedComponent("parent", static () => null, child);
        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        _events.Clear();

        _renderer.Render(null, _container);

        _events.ShouldBe(
        [
            "parent:beforeUnmount",
            "child:beforeUnmount",
            "child:unmounted",
            "parent:unmounted",
        ]);
    }

    [Fact]
    public void UpdateHooks_WrapThePatch_BeforeUpdateSeesPrePatchState()
    {
        var message = Reactive.Reference("one");
        string? subtreeTextDuringBeforeUpdate = null;
        TestElement? rootElement = null;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnBeforeUpdate(() =>
                {
                    // Pre-patch: the host still shows the previous render's content.
                    subtreeTextDuringBeforeUpdate = ((TestText)rootElement!.Children[0]).Text;
                    _events.Add("beforeUpdate");
                });
                Lifecycle.OnUpdated(() =>
                {
                    _events.Add($"updated:{((TestText)rootElement!.Children[0]).Text}");
                });
                return () => VirtualNodeFactory.Element("div", message.Value);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        rootElement = (TestElement)_container.Children[0];

        message.Value = "two";
        _pump.RunUntilIdle();

        subtreeTextDuringBeforeUpdate.ShouldBe("one");
        _events.ShouldBe(["beforeUpdate", "updated:two"]); // Updated observes the patched host
    }

    [Fact]
    public void MultipleRegistrations_RunInRegistrationOrder()
    {
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnMounted(() => _events.Add("first"));
                Lifecycle.OnMounted(() => _events.Add("second"));
                Lifecycle.OnMounted(() => _events.Add("third"));
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        _events.ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public void RegisteringOutsideAnActiveInstance_WarnsAndIgnores()
    {
        using var warnings = new WarningCapture();

        Lifecycle.OnMounted(() => _events.Add("never"));

        warnings.Messages.ShouldContain(message => message.Contains("no active component instance"));
        _events.ShouldBeEmpty();
    }

    [Fact]
    public void UnmountedInstances_NeverFireFurtherUpdateHooks()
    {
        var message = Reactive.Reference("alive");
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnBeforeUpdate(() => _events.Add("beforeUpdate"));
                Lifecycle.OnUpdated(() => _events.Add("updated"));
                return () => VirtualNodeFactory.Element("div", message.Value);
            },
        };
        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        _renderer.Render(null, _container);

        message.Value = "after-unmount";
        _pump.RunUntilIdle();

        _events.ShouldBeEmpty();
    }

    [Fact]
    public void OnErrorCaptured_ReceivesDescendantErrors_UpTheChain()
    {
        Exception? seen = null;
        ComponentInstance? seenSource = null;
        string? seenInfo = null;
        ComponentInstance? childInstance = null;
        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                childInstance = ComponentInstance.Current;
                Lifecycle.OnMounted(static () => throw new InvalidOperationException("hook boom"));
                return static () => VirtualNodeFactory.Text("c");
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnErrorCaptured((exception, source, info) =>
                {
                    seen = exception;
                    seenSource = source;
                    seenInfo = info;
                    return false; // stop propagation
                });
                return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(child));
            },
        };

        Should.NotThrow(() => _renderer.Render(VirtualNodeFactory.Component(parent), _container));

        seen.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("hook boom");
        seenSource.ShouldBeSameAs(childInstance);
        seenInfo.ShouldNotBeNull();
        seenInfo.ShouldContain("Mounted");
    }

    [Fact]
    public void OnErrorCaptured_ReturningTrue_PropagatesToTheNextAncestor()
    {
        var order = new List<string>();
        var child = new TestComponent
        {
            SetupFunction = static (_, _) => static () => throw new InvalidOperationException("render boom"),
        };
        var middle = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnErrorCaptured((_, _, _) =>
                {
                    order.Add("middle");
                    return true; // keep propagating
                });
                return () => VirtualNodeFactory.Element("section", VirtualNodeFactory.Component(child));
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnErrorCaptured((_, _, _) =>
                {
                    order.Add("root");
                    return false;
                });
                return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(middle));
            },
        };

        Should.NotThrow(() => _renderer.Render(VirtualNodeFactory.Component(root), _container));

        order.ShouldBe(["middle", "root"]);
    }

    [Fact]
    public void UnhandledDescendantErrors_SurfaceToTheHost()
    {
        var child = new TestComponent
        {
            SetupFunction = static (_, _) => static () => throw new InvalidOperationException("unhandled boom"),
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(child)),
        };

        Should.Throw<InvalidOperationException>(
                () => _renderer.Render(VirtualNodeFactory.Component(parent), _container))
            .Message.ShouldBe("unhandled boom");
    }

    [Fact]
    public void ActivatedDeactivatedAndServerPrefetch_RegisterOnTheInstance()
    {
        ComponentInstance? instance = null;
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                instance = ComponentInstance.Current;
                Lifecycle.OnActivated(() => { });
                Lifecycle.OnDeactivated(() => { });
                Lifecycle.OnServerPrefetch(static () => System.Threading.Tasks.Task.CompletedTask);
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        // Stored for KeepAlive ([V01.01.03.18]) and the server renderer ([V01.01.07.01]);
        // client-only rendering never invokes them.
        instance!.HasHooks(LifecycleHookKind.Activated).ShouldBeTrue();
        instance.HasHooks(LifecycleHookKind.Deactivated).ShouldBeTrue();
        instance.HasHooks(LifecycleHookKind.ServerPrefetch).ShouldBeTrue();
    }
}
