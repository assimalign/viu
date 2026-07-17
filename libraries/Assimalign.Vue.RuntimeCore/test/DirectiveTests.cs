using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the runtime directive system against @vue/runtime-core's directives.ts —
// https://vuejs.org/guide/reusability/custom-directives.html. Hook firing, ordering, binding data,
// error routing, and component-root transfer, exercised through the in-memory renderer.
public class DirectiveTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;
    private readonly List<string> _events = [];

    public DirectiveTests()
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
    public void AllSevenHooks_FireOnceInOrder_AcrossMountUpdateUnmount()
    {
        var value = Reactive.Reference("v1");
        TestElement? parentAtBeforeMount = null;
        TestElement? parentAtMounted = null;
        var directive = new Directive
        {
            Created = (_, _, _, _) => _events.Add("created"),
            BeforeMount = (element, _, _, _) =>
            {
                _events.Add("beforeMount");
                parentAtBeforeMount = ((TestElement)element!).Parent; // not yet inserted
            },
            Mounted = (element, _, _, _) =>
            {
                _events.Add("mounted");
                parentAtMounted = ((TestElement)element!).Parent; // inserted before mounted (post-flush)
            },
            BeforeUpdate = (_, _, _, _) => _events.Add("beforeUpdate"),
            Updated = (_, _, _, _) => _events.Add("updated"),
            BeforeUnmount = (_, _, _, _) => _events.Add("beforeUnmount"),
            Unmounted = (_, _, _, _) => _events.Add("unmounted"),
        };
        var component = new TestComponent
        {
            SetupFunction = (_, _) => () => Directives.WithDirectives(
                VirtualNodeFactory.Element("div", value.Value), directive, value.Value),
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        _events.ShouldBe(["created", "beforeMount", "mounted"]);
        parentAtBeforeMount.ShouldBeNull();       // beforeMount runs pre-insert
        parentAtMounted.ShouldNotBeNull();        // mounted runs post-insert (post-flush window)

        _events.Clear();
        value.Value = "v2";
        _pump.RunUntilIdle();
        _events.ShouldBe(["beforeUpdate", "updated"]);

        _events.Clear();
        _renderer.Render(null, _container);
        _events.ShouldBe(["beforeUnmount", "unmounted"]);
    }

    [Fact]
    public void Binding_CarriesValueOldValueArgumentModifiersAndInstance()
    {
        var value = Reactive.Reference("first");
        var modifiers = new Dictionary<string, bool> { ["lazy"] = true };
        DirectiveBinding? atMounted = null;
        DirectiveBinding? atUpdated = null;
        ComponentInstance? instance = null;
        var directive = new Directive
        {
            Mounted = (_, binding, _, _) => atMounted = binding,
            Updated = (_, binding, _, _) => atUpdated = binding,
        };
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                instance = ComponentInstance.Current;
                return () => Directives.WithDirectives(
                    VirtualNodeFactory.Element("div", value.Value), directive, value.Value, "color", modifiers);
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        atMounted.ShouldNotBeNull();
        atMounted.Value.ShouldBe("first");
        atMounted.OldValue.ShouldBeNull();
        atMounted.Argument.ShouldBe("color");
        atMounted.Modifiers["lazy"].ShouldBeTrue();
        atMounted.Instance.ShouldBeSameAs(instance);

        value.Value = "second";
        _pump.RunUntilIdle();

        atUpdated.ShouldNotBeNull();
        atUpdated.Value.ShouldBe("second");
        atUpdated.OldValue.ShouldBe("first"); // refreshed from the previous vnode's binding
    }

    [Fact]
    public void HookArguments_AreElementBindingVnodeAndPreviousVnode()
    {
        object? mountedElement = null;
        VirtualNode? mountedVnode = null;
        VirtualNode? updatedPreviousVnode = null;
        var value = Reactive.Reference(1);
        var directive = new Directive
        {
            Mounted = (element, _, node, previous) =>
            {
                mountedElement = element;
                mountedVnode = node;
                previous.ShouldBeNull(); // no previous vnode on mount
            },
            Updated = (_, _, _, previous) => updatedPreviousVnode = previous,
        };
        var component = new TestComponent
        {
            SetupFunction = (_, _) => () => Directives.WithDirectives(
                VirtualNodeFactory.Element("div", value.Value.ToString()), directive),
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        mountedElement.ShouldBeOfType<TestElement>().Tag.ShouldBe("div");
        mountedVnode.ShouldNotBeNull();
        mountedVnode.ElementTag.ShouldBe("div");

        value.Value = 2;
        _pump.RunUntilIdle();

        updatedPreviousVnode.ShouldNotBeNull(); // the update hook receives the replaced vnode
        updatedPreviousVnode.ElementTag.ShouldBe("div");
    }

    [Fact]
    public void ResolveDirective_ResolvesFromTheAppRegistry_AndWarnsOnUnknown()
    {
        using var warnings = new WarningCapture();
        var focus = new Directive { Mounted = (_, _, _, _) => { } };
        IDirective? resolved = null;
        IDirective? unresolved = null;

        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                resolved = Directives.ResolveDirective("focus");
                unresolved = Directives.ResolveDirective("missing");
                return static () => VirtualNodeFactory.Element("div", "x");
            },
        };
        var application = _renderer.Renderer.CreateApplication(root);
        application.Directive("focus", focus);

        application.Mount(_container);

        resolved.ShouldBeSameAs(focus);
        unresolved.ShouldBeNull();
        warnings.Messages.ShouldContain(message => message.Contains("Failed to resolve directive: missing"));
    }

    [Fact]
    public void DirectiveHookException_RoutesThroughErrorHandling_WithoutCorruptingThePipeline()
    {
        Exception? captured = null;
        string? capturedInfo = null;
        var throwingDirective = new Directive
        {
            Mounted = static (_, _, _, _) => throw new InvalidOperationException("directive boom"),
        };
        var child = new TestComponent
        {
            SetupFunction = (_, _) => () => Directives.WithDirectives(
                VirtualNodeFactory.Element("div", "child"), throwingDirective),
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnErrorCaptured((exception, _, info) =>
                {
                    captured = exception;
                    capturedInfo = info;
                    return false; // handled
                });
                return () => VirtualNodeFactory.Element("section", VirtualNodeFactory.Component(child));
            },
        };

        Should.NotThrow(() => _renderer.Render(VirtualNodeFactory.Component(parent), _container));

        captured.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("directive boom");
        capturedInfo.ShouldNotBeNull();
        capturedInfo.ShouldContain("directive");
        // The element still mounted despite the throwing hook — the patch pipeline is intact.
        TestNodeSerializer.Serialize(_container).ShouldContain("child");
    }

    [Fact]
    public void ComponentDirective_TransfersToTheRootElement()
    {
        object? mountedElement = null;
        var directive = new Directive
        {
            Mounted = (element, _, _, _) =>
            {
                _events.Add("mounted");
                mountedElement = element;
            },
        };
        var child = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Element("article", "content"),
        };
        var parent = new TestComponent
        {
            // The directive is applied to the child COMPONENT vnode; it must transfer to <article>.
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div", Directives.WithDirectives(VirtualNodeFactory.Component(child), directive)),
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        _events.ShouldBe(["mounted"]);
        mountedElement.ShouldBeOfType<TestElement>().Tag.ShouldBe("article");
    }

    [Fact]
    public void ComponentDirective_OnNonElementRoot_WarnsInDev()
    {
        using var warnings = new WarningCapture();
        var directive = new Directive { Mounted = (_, _, _, _) => { } };
        var multiRootChild = new TestComponent
        {
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Fragment(
                VirtualNodeFactory.Element("div", "a"),
                VirtualNodeFactory.Element("span", "b")),
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div", Directives.WithDirectives(VirtualNodeFactory.Component(multiRootChild), directive)),
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        warnings.Messages.ShouldContain(message => message.Contains("non-element root"));
    }

    [Fact]
    public void FunctionDirective_RunsTheSameHookOnMountAndUpdate()
    {
        var value = Reactive.Reference("a");
        var runs = 0;
        var directive = Directive.FromFunction((_, _, _, _) => runs++);
        var component = new TestComponent
        {
            SetupFunction = (_, _) => () => Directives.WithDirectives(
                VirtualNodeFactory.Element("div", value.Value), directive, value.Value),
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        runs.ShouldBe(1); // mounted

        value.Value = "b";
        _pump.RunUntilIdle();
        runs.ShouldBe(2); // updated uses the same hook
    }
}
