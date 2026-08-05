using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>Tests the compiler-facing browser render-helper surface.</summary>
public sealed class DomRenderHelpersTests
{
    [Fact]
    public void DirectiveHelpers_AreUnresolvedComponentMetadata()
    {
        DomRenderHelpers._vShow.DirectiveName.ShouldBe("show");
        DomRenderHelpers._vModelText.DirectiveName.ShouldBe("modelText");
        DomRenderHelpers._vModelCheckbox.DirectiveName.ShouldBe("modelCheckbox");
        DomRenderHelpers._vModelRadio.DirectiveName.ShouldBe("modelRadio");
        DomRenderHelpers._vModelSelect.DirectiveName.ShouldBe("modelSelect");
        DomRenderHelpers._vModelDynamic.DirectiveName.ShouldBe("modelDynamic");
    }

    [Fact]
    public void DirectiveHelper_WithDirectives_AttachesImmutableMetadata()
    {
        IComponent element = ComponentTree.Element("input");

        IElementComponent result =
            RenderHelpers._withDirectives(
                element,
                [new object?[] { DomRenderHelpers._vModelText, "value" }])
            .ShouldBeAssignableTo<IElementComponent>();

        result.Directives.Count.ShouldBe(1);
        result.Directives[0].DirectiveName.ShouldBe("modelText");
        result.Directives[0].Value.ShouldBe("value");
    }

    [Fact]
    public void WithDirectives_CarriesTheModifierBag_SoLazyShiftsTheUpdateCarrierToChange()
    {
        // The end-to-end proof of the generated v-model seam: an element assembled exactly the way a
        // compiled render assembles it — the ViuModelBinding carrier in slot 2 and the _createModifiers
        // bag in slot 4 — mounts, and `.lazy` moves the update carrier from `input` to `change`
        // (WHATWG HTML fires `input` per keystroke and `change` on commit:
        // https://html.spec.whatwg.org/multipage/input.html#common-input-element-events). Before
        // [V01.01.05.03.01] the modifier slot was emitted as a property bag, which reads back as no
        // modifiers, so this committed on `input` and the user edit reached the model at the wrong time.
        object? model = "initial";
        using BrowserDirectiveTestHarness harness = new();

        harness.Render(
            RenderHelpers._withDirectives(
                ComponentTree.Element("input"),
                [
                    new object?[]
                    {
                        DomRenderHelpers._vModelText,
                        new ViuModelBinding(model, value => model = value),
                        null,
                        RenderHelpers._createModifiers(("lazy", true)),
                    },
                ]));
        int input = harness.FindElement("input");

        harness.FireInput(input, "typed");
        model.ShouldBe("initial");     // .lazy does not commit on input

        harness.FireChange(input, "typed");
        model.ShouldBe("typed");       // it commits on change
    }

    [Fact]
    public void WithDirectives_PropertyBagInTheModifierSlot_FailsLoudly()
    {
        // The modifier slot is typed name -> bool. A property bag there is a compiler/runtime contract
        // break, and silently reading it as "no modifiers" is exactly what hid [V01.01.05.03.01], so the
        // seam now rejects it instead ([SFC-CG-6]).
        Should.Throw<NotSupportedException>(() =>
            RenderHelpers._withDirectives(
                ComponentTree.Element("input"),
                [
                    new object?[]
                    {
                        DomRenderHelpers._vModelText,
                        new ViuModelBinding("v", _ => { }),
                        null,
                        RenderHelpers._createProps(("lazy", true)),
                    },
                ]));
    }

    [Fact]
    public void TransitionHelpers_LowerToNamedTemplateRequests()
    {
        ITemplateComponent transition =
            RenderHelpers._createVNode(DomRenderHelpers._Transition)
                .ShouldBeAssignableTo<ITemplateComponent>();
        ITemplateComponent group =
            RenderHelpers._createVNode(DomRenderHelpers._TransitionGroup)
                .ShouldBeAssignableTo<ITemplateComponent>();

        transition.TemplateName.ShouldBe("Transition");
        group.TemplateName.ShouldBe("TransitionGroup");
    }

    [Fact]
    public void WithModifiers_PreventAndStop_RecordIntent()
    {
        int invocationCount = 0;
        Action<BrowserEvent> guarded =
            DomRenderHelpers._withModifiers(
                (BrowserEvent _) => invocationCount++,
                "stop",
                "prevent");
        BrowserEvent browserEvent = Event("click");

        guarded(browserEvent);

        invocationCount.ShouldBe(1);
        browserEvent.PropagationStopped.ShouldBeTrue();
        browserEvent.DefaultPrevented.ShouldBeTrue();
    }

    [Fact]
    public void WithKeys_OverModifiers_NestsGuards()
    {
        int invocationCount = 0;
        Action<BrowserEvent> guarded =
            DomRenderHelpers._withKeys(
                DomRenderHelpers._withModifiers(
                    (BrowserEvent _) => invocationCount++,
                    "stop"),
                "enter");

        BrowserEvent enter = Event("keydown", key: "Enter");
        guarded(enter);
        guarded(Event("keydown", key: "a"));

        invocationCount.ShouldBe(1);
        enter.PropagationStopped.ShouldBeTrue();
    }

    [Fact]
    public void WithModifiers_TaskHandler_PreservesReturnedTask()
    {
        TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<BrowserEvent, Task> guarded =
            DomRenderHelpers._withModifiers(
                (BrowserEvent _) => completion.Task,
                "prevent");
        BrowserEvent browserEvent = Event("click");

        Task task = guarded(browserEvent);

        task.ShouldBeSameAs(completion.Task);
        browserEvent.DefaultPrevented.ShouldBeTrue();
    }

    [Fact]
    public void WithKeys_ParameterlessTaskHandler_PreservesReturnedTask()
    {
        TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<BrowserEvent, Task> guarded =
            DomRenderHelpers._withKeys(
                () => completion.Task,
                "enter");

        Task task = guarded(Event("keyup", key: "Enter"));

        task.ShouldBeSameAs(completion.Task);
    }

    [Fact]
    public async Task WithKeys_NonmatchingTaskHandler_DoesNotInvokeHandler()
    {
        int invocationCount = 0;
        Func<BrowserEvent, Task> guarded =
            DomRenderHelpers._withKeys(
                () =>
                {
                    invocationCount++;
                    return Task.CompletedTask;
                },
                "enter");

        await guarded(Event("keyup", key: "Escape"));

        invocationCount.ShouldBe(0);
    }

    private static BrowserEvent Event(string eventName, string key = "")
    {
        return new BrowserEvent(
            eventName,
            0,
            key,
            string.Empty,
            BrowserEventModifiers.None,
            -1,
            0,
            0,
            0,
            0,
            true,
            null,
            false,
            null);
    }
}
