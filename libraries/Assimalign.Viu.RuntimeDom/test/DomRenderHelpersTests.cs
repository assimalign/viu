using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom.Tests;

// Pins the DOM half of the render-helper contract, DomRenderHelpers ([V01.01.04.09]) — the DOM sibling of
// RenderHelpers the .viu generator's second `using static` binds against. Two things are proved:
//   * the by-name facade maps each spelling onto the existing RuntimeDom machinery (the directive
//     singletons and the BrowserEvents modifier/key guards), and the Transition markers fail clearly; and
//   * v-show toggling and a .prevent handler execute end to end through the in-memory DOM adapter
//     (BrowserDirectiveTestHarness) — the execution criterion, DOM-free per the testing rules.
// Upstream parity: @vue/runtime-dom directives/vShow.ts + directives/vModel.ts + modules/events.ts, whose
// behavior the mapped-to types already pin; these tests pin the facade wiring, not the behavior twice.
public sealed class DomRenderHelpersTests : IDisposable
{
    private readonly BrowserDirectiveTestHarness _harness = new();
    private readonly Reference<object?> _visible = Reactive.Reference<object?>(true);

    public void Dispose() => _harness.Dispose();

    // ---- facade -> runtime machinery mapping ---------------------------------------------------

    [Fact]
    public void DirectiveHelpers_AreTheRuntimeDirectiveSingletons()
    {
        // Each _vX facade value is the very directive instance the compiled v-model/v-show transform
        // references, so a withDirectives tuple binds the real directive, never a stand-in.
        DomRenderHelpers._vShow.ShouldBeSameAs(VShow.Instance);
        DomRenderHelpers._vModelText.ShouldBeSameAs(VModelText.Instance);
        DomRenderHelpers._vModelCheckbox.ShouldBeSameAs(VModelCheckbox.Instance);
        DomRenderHelpers._vModelRadio.ShouldBeSameAs(VModelRadio.Instance);
        DomRenderHelpers._vModelSelect.ShouldBeSameAs(VModelSelect.Instance);
        DomRenderHelpers._vModelDynamic.ShouldBeSameAs(VModelDynamic.Instance);
    }

    [Fact]
    public void TransitionHelpers_ResolveToTheRealComponents_AndCreateComponentVnodes()
    {
        // [V01.01.04.07] resolved the markers to the real DOM transition components: passing one as a vnode
        // tag now mounts a component (upstream Transition/TransitionGroup), never throws NotSupportedException.
        DomRenderHelpers._Transition.ShouldBeSameAs(Transition.Instance);
        DomRenderHelpers._TransitionGroup.ShouldBeSameAs(TransitionGroup.Instance);
        RenderHelpers._createVNode(DomRenderHelpers._Transition).Type.ShouldBe(VirtualNodeType.Component);
        RenderHelpers._createVNode(DomRenderHelpers._TransitionGroup).Type.ShouldBe(VirtualNodeType.Component);
    }

    // ---- modifier / key guard wiring (upstream withModifiers / withKeys) -----------------------

    [Fact]
    public void WithModifiers_PreventAndStop_RecordTheirIntentsOnTheEvent()
    {
        var ran = 0;
        // The value-returning inline shape the emitter writes (__event => (expr)) binds Func<BrowserEvent, object?>.
        var guarded = DomRenderHelpers._withModifiers((BrowserEvent browserEvent) => (object?)(++ran), "stop", "prevent");
        var browserEvent = Event("click");

        guarded(browserEvent);

        ran.ShouldBe(1);
        browserEvent.PropagationStopped.ShouldBeTrue(); // .stop
        browserEvent.DefaultPrevented.ShouldBeTrue();   // .prevent
    }

    [Fact]
    public void WithModifiers_ParameterlessVoidHandler_IsGuarded()
    {
        var ran = 0;
        // The void parameterless method-group shape (@click.prevent="submit", void submit()) binds Action.
        var guarded = DomRenderHelpers._withModifiers(() => ran++, "prevent");
        var browserEvent = Event("click");

        guarded(browserEvent);

        ran.ShouldBe(1);
        browserEvent.DefaultPrevented.ShouldBeTrue();
    }

    [Fact]
    public void WithKeys_RunsOnlyForTheNamedKey()
    {
        var ran = 0;
        var guarded = DomRenderHelpers._withKeys((BrowserEvent browserEvent) => { ran++; }, "enter");

        guarded(Event("keyup", key: "Enter")); // event.key "Enter" hyphenates to "enter"
        ran.ShouldBe(1);

        guarded(Event("keyup", key: "Escape")); // a non-matching key is skipped
        ran.ShouldBe(1);
    }

    [Fact]
    public void WithKeys_OverModifiers_NestAsTheEmitterStacksThem()
    {
        // @keydown.enter.stop stacks the guards: _withKeys(_withModifiers(handler, ["stop"]), ["enter"]);
        // the inner call returns Action<BrowserEvent>, which the outer _withKeys overload accepts.
        var ran = 0;
        var guarded = DomRenderHelpers._withKeys(
            DomRenderHelpers._withModifiers((BrowserEvent browserEvent) => { ran++; }, "stop"),
            "enter");

        var enter = Event("keydown", key: "Enter");
        guarded(enter);
        ran.ShouldBe(1);
        enter.PropagationStopped.ShouldBeTrue(); // the nested .stop still applied

        guarded(Event("keydown", key: "a")); // wrong key: neither the handler nor the inner .stop runs
        ran.ShouldBe(1);
    }

    // ---- end-to-end execution through the in-memory DOM adapter --------------------------------

    [Fact]
    public void VShowFacade_TogglesInlineDisplay_ThroughTheAdapter()
    {
        // The _vShow facade drives the real VShow directive through the renderer + in-memory adapter: a
        // truthy binding leaves no inline display, a falsy binding sets display:none, and it toggles back.
        _visible.Value = true;
        _harness.Render(Shown());
        _harness.RunUntilIdle();
        var div = _harness.FindElement("div");
        _harness.Display(div).ShouldBeNull();

        _visible.Value = false;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBe("none");

        _visible.Value = true;
        _harness.RunUntilIdle();
        _harness.Display(div).ShouldBeNull();
    }

    [Fact]
    public void WithModifiersPreventHandler_RunsAndAppliesPreventDefault_ThroughTheAdapter()
    {
        // A @click.prevent handler wired through the facade: mounting registers the guarded Action<BrowserEvent>
        // as the onClick listener, firing a click runs the handler AND records preventDefault, and the bridge
        // response flags carry bit 1 back to the live event — the .prevent execution criterion, DOM-free.
        var ran = 0;
        BrowserEvent? captured = null;
        var component = new RenderComponent((_, _) => () =>
        {
            var properties = new VirtualNodeProperties();
            properties.Set("onClick", DomRenderHelpers._withModifiers(
                (BrowserEvent browserEvent) => { ran++; captured = browserEvent; },
                "prevent"));
            return VirtualNodeFactory.Element("button", properties, "go");
        });

        _harness.Render(component);
        _harness.RunUntilIdle();
        var button = _harness.FindElement("button");

        var flags = _harness.FireClick(button);

        ran.ShouldBe(1);                            // the guarded handler ran exactly once
        captured.ShouldNotBeNull();
        captured!.DefaultPrevented.ShouldBeTrue();  // .prevent recorded preventDefault on the event
        (flags & 2).ShouldBe(2);                    // bit 1 = preventDefault, applied to the live event by the bridge
    }

    // A component whose render binds _vShow to the reactive condition, mirroring the compiled v-show output.
    private RenderComponent Shown()
        => new((_, _) => () => Directives.WithDirectives(
            VirtualNodeFactory.Element("div", null, "content"),
            DomRenderHelpers._vShow,
            _visible.Value));

    private static BrowserEvent Event(string eventName, string key = "")
        => new(eventName, 0, key, string.Empty, BrowserEventModifiers.None, -1, 0, 0, 0, 0, true, null, false, null);
}
