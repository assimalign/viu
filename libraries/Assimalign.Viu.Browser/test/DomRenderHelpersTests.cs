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
