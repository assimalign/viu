using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser's qualified directive tokens and host-local command path [CMP-7], [SFC-CG-6..7].
public sealed class BrowserDirectiveIntegrationTests
{
    [Fact]
    public void TextModel_Mount_RegistersListenersAndReflectsTheInitialValue()
    {
        object? assigned = null;
        DirectiveInvocation directive = new(
            typeof(VModelText),
            new ViuModelBinding("initial text", value => assigned = value, ["trim"]));
        ElementNode input = new(
            new QualifiedName("input"),
            directives: [directive]);

        string frameText = RenderAndReadFrames(input);

        frameText.ShouldContain("initial text");
        frameText.ShouldContain("input");
        assigned.ShouldBeNull();
    }

    [Fact]
    public void TextModel_CompositionEvents_SuppressInputThenCommitWithModifiers()
    {
        object? assigned = null;
        int elementHandle = 0;
        ElementNode input = new(
            new QualifiedName("input"),
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelText),
                    new ViuModelBinding(
                        string.Empty,
                        value => assigned = value,
                        ["trim", "number"])),
            ],
            mountReference: value => elementHandle = value is int handle ? handle : 0);

        RenderAndDispatch(
            input,
            host =>
            {
                host.DispatchEvent(
                    elementHandle,
                    capture: false,
                    CreateBrowserEvent("compositionstart"));
                host.DispatchEvent(
                    elementHandle,
                    capture: false,
                    CreateBrowserEvent("input", targetValue: "ignored"));
                assigned.ShouldBeNull();
                host.DispatchEvent(
                    elementHandle,
                    capture: false,
                    CreateBrowserEvent("compositionend", targetValue: " 42 "));
            });

        assigned.ShouldBeOfType<double>().ShouldBe(42d);
    }

    [Fact]
    public void CheckboxModel_Mount_ReflectsLooseListMembership()
    {
        DirectiveInvocation directive = new(
            typeof(VModelCheckbox),
            new ViuModelBinding(new object?[] { 1 }, _ => { }));
        ElementNode input = new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "checkbox"),
                ElementBinding.Property("value", "1"),
            ],
            directives: [directive]);

        string frameText = RenderAndReadFrames(input);

        frameText.ShouldContain("checked");
    }

    [Fact]
    public void CheckboxModel_ChangeEvent_InvokesGeneratedSetterWithRawTrueValue()
    {
        object? assigned = null;
        int elementHandle = 0;
        object rawTrueValue = new();
        ElementNode input = new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "checkbox"),
                ElementBinding.Property("true-value", rawTrueValue),
                ElementBinding.Property("false-value", "unchecked"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelCheckbox),
                    new ViuModelBinding("unchecked", value => assigned = value)),
            ],
            mountReference: value => elementHandle = value is int handle ? handle : 0);

        RenderAndDispatch(
            input,
            host => host.DispatchEvent(
                elementHandle,
                capture: false,
                CreateBrowserEvent("change", targetChecked: true)));

        assigned.ShouldBeSameAs(rawTrueValue);
    }

    [Fact]
    public void RadioModel_Mount_ReflectsRawValueEquality()
    {
        ElementNode input = new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "radio"),
                ElementBinding.Property("value", "chosen"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelRadio),
                    new ViuModelBinding("chosen", _ => { })),
            ]);

        string frameText = RenderAndReadFrames(input);

        frameText.ShouldContain("checked");
    }

    [Fact]
    public void RadioModel_ChangeEvent_InvokesGeneratedSetterWithRawElementValue()
    {
        object? assigned = null;
        int elementHandle = 0;
        object rawValue = new();
        ElementNode input = new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "radio"),
                ElementBinding.Property("value", rawValue),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelRadio),
                    new ViuModelBinding(null, value => assigned = value)),
            ],
            mountReference: value => elementHandle = value is int handle ? handle : 0);

        RenderAndDispatch(
            input,
            host => host.DispatchEvent(
                elementHandle,
                capture: false,
                CreateBrowserEvent("change")));

        assigned.ShouldBeSameAs(rawValue);
    }

    [Fact]
    public void RadioModel_UpdateWithStableModelAndChangedValue_RecomputesChecked()
    {
        ElementNode Radio(object? value) => new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "radio"),
                ElementBinding.Property("value", value),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelRadio),
                    new ViuModelBinding("chosen", _ => { })),
            ]);

        string updateFrameText = RenderUpdateAndReadFrames(
            Radio("chosen"),
            Radio("other"));

        updateFrameText.ShouldContain("checked");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckboxModel_UpdateWithStableScalarAndChangedRawValues_RecomputesChecked(
        bool changeTrueValue)
    {
        ElementNode Checkbox(bool updated) => new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "checkbox"),
                ElementBinding.Property(
                    "true-value",
                    changeTrueValue && updated ? "other" : "chosen"),
                ElementBinding.Property(
                    "false-value",
                    !changeTrueValue && updated ? "other" : "unchosen"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelCheckbox),
                    new ViuModelBinding("chosen", _ => { })),
            ]);

        string updateFrameText = RenderUpdateAndReadFrames(
            Checkbox(updated: false),
            Checkbox(updated: true));

        updateFrameText.ShouldContain("checked");
    }

    [Fact]
    public void SelectModel_Mount_ReflectsSelectionThroughDescendantHandles()
    {
        ElementNode select = new(
            new QualifiedName("select"),
            children:
            [
                new ElementNode(
                    new QualifiedName("option"),
                    bindings: [ElementBinding.Property("value", "first")]),
                new ElementNode(
                    new QualifiedName("option"),
                    bindings: [ElementBinding.Property("value", "second")]),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelSelect),
                    new ViuModelBinding("second", _ => { })),
            ]);

        string frameText = RenderAndReadFrames(select);

        frameText.ShouldContain("selected");
    }

    [Fact]
    public void SelectModel_ChangeEvent_MapsBrowserValueBackToRawOptionValue()
    {
        object? assigned = null;
        int elementHandle = 0;
        object rawValue = new();
        ElementNode select = new(
            new QualifiedName("select"),
            children:
            [
                new ElementNode(
                    new QualifiedName("option"),
                    bindings: [ElementBinding.Property("value", rawValue)]),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelSelect),
                    new ViuModelBinding(null, value => assigned = value)),
            ],
            mountReference: value => elementHandle = value is int handle ? handle : 0);

        RenderAndDispatch(
            select,
            host => host.DispatchEvent(
                elementHandle,
                capture: false,
                CreateBrowserEvent(
                    "change",
                    targetValue: rawValue.ToString())));

        assigned.ShouldBeSameAs(rawValue);
    }

    [Fact]
    public void ShowAndCssVariables_Mount_ApplyElementLocalStylesInTheSameCommit()
    {
        ElementNode element = new(
            new QualifiedName("section"),
            directives:
            [
                new DirectiveInvocation(typeof(VShow), false),
                CssVariables.Bind(new Dictionary<string, string>
                {
                    ["theme-color"] = "rebeccapurple",
                }),
            ]);

        string frameText = RenderAndReadFrames(element);

        frameText.ShouldContain("display");
        frameText.ShouldContain("none");
        frameText.ShouldContain("--theme-color");
        frameText.ShouldContain("rebeccapurple");
    }

    [Fact]
    public void DynamicModel_FileInput_RejectsProgrammaticTwoWayBinding()
    {
        ElementNode input = new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "file"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelDynamic),
                    new ViuModelBinding(null, _ => { })),
            ]);
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(900);
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ApplicationContext application = CreateApplication(input);

            Action render = () => renderer.Render(input, 900, application);

            InvalidOperationException exception = render.ShouldThrow<InvalidOperationException>();
            exception.Message.ShouldContain("file input");
            Drain(ref scheduledFlush);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void DynamicModel_TypeSwitch_RemovesOldListenersAndMountsNewBehavior()
    {
        ElementNode TextInput() => new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "text"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelDynamic),
                    new ViuModelBinding("editing", _ => { })),
            ]);
        ElementNode CheckboxInput() => new(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "checkbox"),
                ElementBinding.Property("value", "editing"),
            ],
            directives:
            [
                new DirectiveInvocation(
                    typeof(VModelDynamic),
                    new ViuModelBinding(new object?[] { "editing" }, _ => { })),
            ]);
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            List<byte[]> frames = [];
            var host = new BrowserRendererHost(
                (frame, length) =>
                {
                    frames.Add(frame.AsSpan(0, length).ToArray());
                    return [];
                });
            host.ObserveForeignHandle(950);
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ElementNode initial = TextInput();
            ApplicationContext application = CreateApplication(initial);
            renderer.Render(initial, 950, application);
            Drain(ref scheduledFlush);
            frames.Clear();

            renderer.Render(CheckboxInput(), 950, application);
            Drain(ref scheduledFlush);

            string frameText = Encoding.UTF8.GetString(
                frames.SelectMany(static frame => frame).ToArray());
            frameText.ShouldContain("input");
            frameText.ShouldContain("compositionstart");
            frameText.ShouldContain("focus");
            frameText.ShouldContain("checked");
            renderer.Render(null, 950, application);
            Drain(ref scheduledFlush);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static string RenderAndReadFrames(VirtualNode root)
    {
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            List<byte[]> frames = [];
            var host = new BrowserRendererHost(
                (frame, length) =>
                {
                    frames.Add(frame.AsSpan(0, length).ToArray());
                    return [];
                });
            host.ObserveForeignHandle(800);
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ApplicationContext application = CreateApplication(root);

            renderer.Render(root, 800, application);
            Drain(ref scheduledFlush);
            renderer.Render(null, 800, application);
            Drain(ref scheduledFlush);

            frames.ShouldNotBeEmpty();
            return Encoding.UTF8.GetString(
                frames.SelectMany(static frame => frame).ToArray());
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static string RenderUpdateAndReadFrames(
        VirtualNode initial,
        VirtualNode updated)
    {
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            List<byte[]> frames = [];
            var host = new BrowserRendererHost(
                (frame, length) =>
                {
                    frames.Add(frame.AsSpan(0, length).ToArray());
                    return [];
                });
            host.ObserveForeignHandle(825);
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ApplicationContext application = CreateApplication(initial);
            renderer.Render(initial, 825, application);
            Drain(ref scheduledFlush);
            frames.Clear();

            renderer.Render(updated, 825, application);
            Drain(ref scheduledFlush);
            string frameText = Encoding.UTF8.GetString(
                frames.SelectMany(static frame => frame).ToArray());

            renderer.Render(null, 825, application);
            Drain(ref scheduledFlush);
            return frameText;
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static void RenderAndDispatch(
        VirtualNode root,
        Action<BrowserRendererHost> dispatch)
    {
        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            var host = new BrowserRendererHost((_, _) => []);
            host.ObserveForeignHandle(850);
            using IDisposable activation = host.Activate();
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            ApplicationContext application = CreateApplication(root);
            renderer.Render(root, 850, application);
            Drain(ref scheduledFlush);

            dispatch(host);

            renderer.Render(null, 850, application);
            Drain(ref scheduledFlush);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static BrowserEvent CreateBrowserEvent(
        string eventName,
        string? targetValue = null,
        bool targetChecked = false,
        string[]? selectedValues = null) => new(
            eventName,
            timeStamp: 0,
            key: string.Empty,
            code: string.Empty,
            BrowserEventModifiers.None,
            button: -1,
            buttons: 0,
            clientX: 0,
            clientY: 0,
            detail: 0,
            isSelfTarget: true,
            targetValue,
            targetChecked,
            selectedValues);

    private static void Drain(ref Action? scheduledFlush)
    {
        while (scheduledFlush is { } flush)
        {
            scheduledFlush = null;
            flush();
        }
    }

    private static ApplicationContext CreateApplication(VirtualNode root) => new(
        new ApplicationOptions
        {
            RootComponent = root,
            Directives = new BrowserTokenResolver(),
        });

    private sealed class BrowserTokenResolver : IDirectiveResolver
    {
        public IDirective? Resolve(Type directiveType)
        {
            if (directiveType == typeof(VModelText))
            {
                return VModelText.Instance;
            }

            if (directiveType == typeof(VModelCheckbox))
            {
                return VModelCheckbox.Instance;
            }

            if (directiveType == typeof(VModelRadio))
            {
                return VModelRadio.Instance;
            }

            if (directiveType == typeof(VModelSelect))
            {
                return VModelSelect.Instance;
            }

            if (directiveType == typeof(VModelDynamic))
            {
                return VModelDynamic.Instance;
            }

            if (directiveType == typeof(VShow))
            {
                return VShow.Instance;
            }

            return directiveType == typeof(CssVariables) ? CssVariables.Instance : null;
        }
    }
}
