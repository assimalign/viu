using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Testing;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the component event contract of @vue/runtime-core's componentEmits.ts —
// https://vuejs.org/guide/components/events.html and /guide/components/v-model.html.
public class ComponentEmitTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ComponentEmitTests()
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

    private ComponentSetupContext MountAndCaptureContext(TestComponent component, VirtualNodeProperties? componentProperties)
    {
        ComponentSetupContext? context = null;
        var capturing = new TestComponent
        {
            Name = component.Name,
            Properties = component.Properties,
            Emits = component.Emits,
            SetupFunction = (properties, setupContext) =>
            {
                context = setupContext;
                return component.SetupFunction(properties, setupContext);
            },
        };
        _renderer.Render(
            VirtualNodeFactory.Element("main", VirtualNodeFactory.Component(capturing, componentProperties)),
            _container);
        return context!;
    }

    [Fact]
    public void Emit_InvokesTheMatchingHandlerProp_WithArguments()
    {
        object? received = null;
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("change")],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var context = MountAndCaptureContext(child, VirtualNodeFactory.Properties(
            ("onChange", (Action<object?>)(value => received = value))));

        context.Emit("change", 42);

        received.ShouldBe(42);
    }

    [Fact]
    public void Emit_KebabCaseEventMatchesCamelCaseHandler()
    {
        var calls = 0;
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("item-selected")],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var context = MountAndCaptureContext(child, VirtualNodeFactory.Properties(
            ("onItemSelected", (Action)(() => calls++))));

        context.Emit("item-selected");

        calls.ShouldBe(1);
    }

    [Fact]
    public void Emit_SupportsSpreadArgumentHandlers()
    {
        object?[]? received = null;
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("move")],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var context = MountAndCaptureContext(child, VirtualNodeFactory.Properties(
            ("onMove", (Action<object?[]>)(arguments => received = arguments))));

        context.Emit("move", 3, 4);

        received.ShouldBe(new object?[] { 3, 4 });
    }

    [Fact]
    public void UpdateModelValue_RoundTrips_TheVModelContract()
    {
        // The runtime half of v-model: emitting update:modelValue invokes onUpdate:modelValue.
        object? updated = null;
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("update:modelValue")],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var context = MountAndCaptureContext(child, VirtualNodeFactory.Properties(
            ("onUpdate:modelValue", (Action<object?>)(value => updated = value))));

        context.Emit("update:modelValue", "typed");

        updated.ShouldBe("typed");
    }

    [Fact]
    public void OnceHandlers_FireExactlyOncePerInstance_AcrossReRenders()
    {
        var onceCalls = 0;
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("save")],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var revision = Reactive.Reference(0);
        ComponentSetupContext? context = null;
        var capturing = new TestComponent
        {
            Emits = child.Emits,
            SetupFunction = (properties, setupContext) =>
            {
                context = setupContext;
                return static () => VirtualNodeFactory.Text("child");
            },
        };
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Text(revision.Value.ToString()),
                VirtualNodeFactory.Component(capturing, VirtualNodeFactory.Properties(
                    ("onSaveOnce", (Action)(() => onceCalls++)),
                    ("revision", revision.Value)))),
        };
        _renderer.Render(VirtualNodeFactory.Component(parent), _container);

        context!.Emit("save");
        context.Emit("save");
        onceCalls.ShouldBe(1);

        // Re-render the parent (new handler prop instance) — once-tracking is per component
        // instance, not per render (upstream emitted-tracking parity).
        revision.Value = 1;
        _pump.RunUntilIdle();
        context.Emit("save");

        onceCalls.ShouldBe(1);
    }

    [Fact]
    public void UndeclaredEmit_Warns_AndValidatorFailuresWarn()
    {
        using var warnings = new WarningCapture();
        var child = new TestComponent
        {
            Name = "Emitter",
            Emits =
            [
                new ComponentEmitDefinition("valid") { Validator = arguments => arguments.Length == 1 },
            ],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };
        var context = MountAndCaptureContext(child, null);

        context.Emit("undeclared");
        context.Emit("valid"); // zero args → validator false

        warnings.Messages.ShouldContain(message =>
            message.Contains("undeclared") && message.Contains("Emitter"));
        warnings.Messages.ShouldContain(message =>
            message.Contains("event validation failed") && message.Contains("valid"));
    }

    [Fact]
    public void DeclaredEmitHandlers_AreExcludedFromAttrsFallthrough()
    {
        var child = new TestComponent
        {
            Emits = [new ComponentEmitDefinition("save")],
            SetupFunction = static (_, _) => static () =>
                VirtualNodeFactory.Element("button", "press"),
        };

        _renderer.Render(
            VirtualNodeFactory.Element(
                "main",
                VirtualNodeFactory.Component(child, VirtualNodeFactory.Properties(
                    ("onSave", (Action)(() => { })),
                    ("onSaveOnce", (Action)(() => { })),
                    ("data-keep", "yes")))),
            _container);

        var main = (TestElement)_container.Children[0];
        var button = (TestElement)main.Children[0];
        // The declared emit's handlers did NOT fall through; the plain attribute did.
        button.Properties.ContainsKey("onSave").ShouldBeFalse();
        button.Properties.ContainsKey("onSaveOnce").ShouldBeFalse();
        button.Properties["data-keep"].ShouldBe("yes");
    }
}
