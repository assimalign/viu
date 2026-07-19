using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins <component :is> resolution and replace-on-change against @vue/runtime-core's
// resolveDynamicComponent (helpers/resolveAssets.ts) — https://vuejs.org/api/built-in-special-elements.html#component.
public class DynamicComponentTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;
    private readonly List<string> _events = [];

    public DynamicComponentTests()
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

    private TestComponent LifecycleComponent(string name) => new()
    {
        Name = name,
        SetupFunction = (_, _) =>
        {
            Lifecycle.OnMounted(() => _events.Add($"{name}:mounted"));
            Lifecycle.OnUnmounted(() => _events.Add($"{name}:unmounted"));
            return () => VirtualNodeFactory.Element("div", name);
        },
    };

    [Fact]
    public void ResolveDynamicComponent_ReturnsAComponentDefinitionUnchanged()
    {
        var definition = LifecycleComponent("A");

        DynamicComponents.ResolveDynamicComponent(definition).ShouldBeSameAs(definition);
    }

    [Fact]
    public void ResolveDynamicComponent_UnresolvedString_FallsBackToTheTag_WithoutWarning()
    {
        using var warnings = new WarningCapture();

        // No active instance / no registration: the string is returned as an element tag, silently
        // (upstream resolveDynamicComponent uses warnMissing=false).
        DynamicComponents.ResolveDynamicComponent("div").ShouldBe("div");
        warnings.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveDynamicComponent_String_ResolvesAgainstTheAppRegistry_DuringRender()
    {
        var widget = LifecycleComponent("widget");
        object? resolvedRegistered = null;
        object? resolvedUnknown = null;

        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                resolvedRegistered = DynamicComponents.ResolveDynamicComponent("my-widget");
                resolvedUnknown = DynamicComponents.ResolveDynamicComponent("section");
                return static () => VirtualNodeFactory.Element("div", "root");
            },
        };
        var application = _renderer.Renderer.CreateApplication(root);
        application.Component("my-widget", widget);

        application.Mount(_container);

        resolvedRegistered.ShouldBeSameAs(widget);       // registered name resolves to the definition
        resolvedUnknown.ShouldBe("section");             // unregistered name falls back to a tag
    }

    [Fact]
    public void DynamicComponent_BuildsComponentElementOrCommentVnodes()
    {
        var definition = LifecycleComponent("A");

        var componentVnode = DynamicComponents.DynamicComponent(definition);
        componentVnode.Type.ShouldBe(VirtualNodeType.Component);
        componentVnode.ComponentType.ShouldBeSameAs(definition);

        var elementVnode = DynamicComponents.DynamicComponent("span");
        elementVnode.Type.ShouldBe(VirtualNodeType.Element);
        elementVnode.ElementTag.ShouldBe("span");

        var commentVnode = DynamicComponents.DynamicComponent(null);
        commentVnode.Type.ShouldBe(VirtualNodeType.Comment);
    }

    [Fact]
    public void ChangingIs_FullyUnmountsTheOldComponent_AndMountsTheNew_NoStateBleed()
    {
        var componentA = LifecycleComponent("A");
        var componentB = LifecycleComponent("B");
        var isValue = Reactive.Reference<object?>(componentA);

        var host = new TestComponent
        {
            SetupFunction = (_, _) => () => DynamicComponents.DynamicComponent(isValue.Value),
        };

        _renderer.Render(VirtualNodeFactory.Component(host), _container);
        _events.ShouldBe(["A:mounted"]);
        TestNodeSerializer.Serialize(_container).ShouldContain("A");

        isValue.Value = componentB;
        _pump.RunUntilIdle();

        // The old component fully unmounts (its Unmounted hook fires) before the new one mounts —
        // a keyed-swap-style replace, no lifecycle bleed.
        _events.ShouldBe(["A:mounted", "A:unmounted", "B:mounted"]);
        var html = TestNodeSerializer.Serialize(_container);
        html.ShouldContain("B");
        html.ShouldNotContain(">A<");
    }

    [Fact]
    public void ChangingIs_FromComponentToElementTag_ReplacesTheTree()
    {
        var componentA = LifecycleComponent("A");
        var isValue = Reactive.Reference<object?>(componentA);

        var host = new TestComponent
        {
            SetupFunction = (_, _) => () => DynamicComponents.DynamicComponent(isValue.Value),
        };

        _renderer.Render(VirtualNodeFactory.Component(host), _container);
        _events.ShouldBe(["A:mounted"]);

        isValue.Value = "hr"; // an element tag (no registered "hr")
        _pump.RunUntilIdle();

        _events.ShouldBe(["A:mounted", "A:unmounted"]);
        TestNodeSerializer.Serialize(_container).ShouldContain("<hr>");
    }
}
