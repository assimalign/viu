using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>
/// Pins the explicit-context <see cref="CssVariables.UseCssVariables"/> runtime — the C# port of
/// Vue 3.5's <c>useCssVars</c>. Tests run DOM-free over the redesigned component tree.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class CssVariablesTests
{
    [Fact]
    public void UseCssVariables_AfterMount_AppliesCustomPropertyToRoot()
    {
        Reference<string> color = Reactive.Reference("red");
        CssVariableTemplate template =
            new(() => Variables(("abc12345", color.Value)));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);

        harness.Render(Request());
        harness.RunUntilIdle();

        int element = harness.FindElement("div");
        harness.CssVariable(element, "--abc12345").ShouldBe("red");
        harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVariables_ReactiveDependencyChanges_UpdatesWithoutRendering()
    {
        Reference<string> color = Reactive.Reference("red");
        int renderCount = 0;
        CssVariableTemplate template =
            new(
                () => Variables(("abc12345", color.Value)),
                onRender: () => renderCount++);
        using BrowserDirectiveTestHarness harness = CreateHarness(template);
        harness.Render(Request());
        harness.RunUntilIdle();
        int element = harness.FindElement("div");

        color.Value = "blue";
        harness.RunUntilIdle();

        harness.CssVariable(element, "--abc12345").ShouldBe("blue");
        harness.CssVariableCrossings.ShouldBe(2);
        renderCount.ShouldBe(1);
    }

    [Fact]
    public void UseCssVariables_GeneratorShapedReferenceGetter_TracksRuns()
    {
        Reference<int> count = Reactive.Reference(1);
        int getterRuns = 0;
        CssVariableTemplate template =
            new(
                () =>
                {
                    getterRuns++;
                    return Variables(
                        (
                            "width",
                            Convert.ToString(
                                (object?)count.Value,
                                CultureInfo.InvariantCulture)
                                ?? string.Empty));
                });
        using BrowserDirectiveTestHarness harness = CreateHarness(template);
        harness.Render(Request());
        harness.RunUntilIdle();
        int element = harness.FindElement("div");

        harness.CssVariable(element, "--width").ShouldBe("1");
        getterRuns.ShouldBe(1);
        count.Value = 42;
        harness.RunUntilIdle();

        harness.CssVariable(element, "--width").ShouldBe("42");
        getterRuns.ShouldBe(2);
        harness.CssVariableCrossings.ShouldBe(2);
    }

    [Fact]
    public void UseCssVariables_UnrelatedReactiveChange_DoesNotReapply()
    {
        Reference<string> color = Reactive.Reference("red");
        Reference<int> unrelated = Reactive.Reference(0);
        CssVariableTemplate template =
            new(() => Variables(("color", color.Value)));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);
        harness.Render(Request());
        harness.RunUntilIdle();

        unrelated.Value = 1;
        harness.RunUntilIdle();

        harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVariables_MultipleProperties_BatchesOneOperationPerElement()
    {
        Reference<string> color = Reactive.Reference("red");
        Reference<string> size = Reactive.Reference("10px");
        CssVariableTemplate template =
            new(
                () => Variables(
                    ("color", color.Value),
                    ("size", size.Value)));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);

        harness.Render(Request());
        harness.RunUntilIdle();

        int element = harness.FindElement("div");
        harness.CssVariable(element, "--color").ShouldBe("red");
        harness.CssVariable(element, "--size").ShouldBe("10px");
        harness.CssVariableCrossings.ShouldBe(1);
    }

    [Fact]
    public void UseCssVariables_FragmentRoot_AppliesToEveryOutermostElement()
    {
        Reference<string> color = Reactive.Reference("red");
        CssVariableTemplate template =
            new(
                () => Variables(("value", color.Value)),
                render: static () =>
                    ComponentTree.Fragment(
                    [
                        ComponentTree.Element(
                            "div",
                            children: [ComponentTree.Text("first")]),
                        ComponentTree.Element(
                            "span",
                            children: [ComponentTree.Text("second")]),
                    ]));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);

        harness.Render(Request());
        harness.RunUntilIdle();

        harness.CssVariable(harness.FindElement("div"), "--value")
            .ShouldBe("red");
        harness.CssVariable(harness.FindElement("span"), "--value")
            .ShouldBe("red");
        harness.CssVariableCrossings.ShouldBe(2);
    }

    [Fact]
    public void UseCssVariables_ComponentRootChanges_ReappliesToReplacementElement()
    {
        Reference<bool> showAlternative = Reactive.Reference(false);
        CssVariableTemplate template =
            new(
                () => Variables(("color", "red")),
                render: () =>
                    ComponentTree.Element(
                        showAlternative.Value ? "span" : "div",
                        children: [ComponentTree.Text("content")]));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);
        harness.Render(Request());
        harness.RunUntilIdle();

        showAlternative.Value = true;
        harness.RunUntilIdle();

        int replacement = harness.FindElement("span");
        harness.CssVariable(replacement, "--color").ShouldBe("red");
        harness.CssVariableCrossings.ShouldBe(2);
    }

    [Fact]
    public void UseCssVariables_ComponentUnmounts_StopsWatcher()
    {
        Reference<string> color = Reactive.Reference("red");
        CssVariableTemplate template =
            new(() => Variables(("color", color.Value)));
        using BrowserDirectiveTestHarness harness = CreateHarness(template);
        harness.Render(Request());
        harness.RunUntilIdle();
        harness.Unmount();
        harness.RunUntilIdle();
        int crossingsAfterUnmount = harness.CssVariableCrossings;

        color.Value = "blue";
        harness.RunUntilIdle();

        harness.CssVariableCrossings.ShouldBe(crossingsAfterUnmount);
    }

    [Fact]
    public void UseCssVariables_NullContext_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => CssVariables.UseCssVariables(
                null!,
                () => Variables(("value", "1"))));
    }

    [Fact]
    public void UseCssVariables_NullGetter_Throws()
    {
        PlainTemplate template = new();
        IComponentFactory factory =
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(PlainTemplate),
                    () => template),
            ]);
        using BrowserDirectiveTestHarness harness = new(factory);
        IComponentContext context =
            harness.Render(ComponentTree.Template<PlainTemplate>())
                .ShouldNotBeNull();

        Should.Throw<ArgumentNullException>(
            () => CssVariables.UseCssVariables(context, null!));
    }

    [Fact]
    public void UseCssVariables_BufferedMode_PostFlushCommitsCssMutation()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        Reference<string> color = Reactive.Reference("red");
        CssVariableTemplate template =
            new(() => Variables(("color", color.Value)));
        ITemplateComponent request = Request();
        IComponentFactory factory =
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(CssVariableTemplate),
                    () => template),
            ]);
        IApplicationContext application =
            new ApplicationContext(
                request,
                factory,
                new EmptyServiceProvider(),
                directives: BrowserDirectiveResolver.Instance);
        InMemoryHandleDom dom = new();
        int container = dom.CreateElement("root", null);
        BufferedBrowserNodeOperations buffered =
            new(
                (frame, length) =>
                    CommandBufferDecoder.Apply(frame, length, dom),
                static _ => 0,
                dom.ParentNode,
                dom.NextSibling,
                dom.InsertStaticContent);
        buffered.Activate();
        buffered.ObserveForeignHandle(container);
        Renderer<int> renderer =
            RendererFactory.CreateRenderer(buffered.Create());

        try
        {
            renderer.Render(request, container, application);
            pump.RunUntilIdle();

            dom.Serialize(container)
                .ShouldContain("style.--color=\"red\"");
            int crossingsAfterMount = buffered.InteropCallCount;
            buffered.Buffer.HasPendingOperations.ShouldBeFalse();

            color.Value = "blue";
            pump.RunUntilIdle();

            dom.Serialize(container)
                .ShouldContain("style.--color=\"blue\"");
            buffered.InteropCallCount.ShouldBe(crossingsAfterMount + 1);
            buffered.Buffer.HasPendingOperations.ShouldBeFalse();

            renderer.Render(null, container, application);
            pump.RunUntilIdle();
        }
        finally
        {
            buffered.Deactivate();
        }
    }

    private static BrowserDirectiveTestHarness CreateHarness(
        CssVariableTemplate template)
    {
        IComponentFactory factory =
            new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(CssVariableTemplate),
                    () => template),
            ]);
        return new BrowserDirectiveTestHarness(factory);
    }

    private static ITemplateComponent Request()
        => ComponentTree.Template<CssVariableTemplate>();

    private static IReadOnlyDictionary<string, string> Variables(
        params (string Name, string Value)[] entries)
    {
        Dictionary<string, string> variables =
            new(StringComparer.Ordinal);
        for (int index = 0; index < entries.Length; index++)
        {
            variables[entries[index].Name] = entries[index].Value;
        }

        return variables;
    }

    private sealed class CssVariableTemplate : IComponentTemplate
    {
        private readonly Func<IReadOnlyDictionary<string, string>> _getter;
        private readonly Action? _onRender;
        private readonly Func<IComponent> _render;

        internal CssVariableTemplate(
            Func<IReadOnlyDictionary<string, string>> getter,
            Action? onRender = null,
            Func<IComponent>? render = null)
        {
            _getter = getter;
            _onRender = onRender;
            _render =
                render
                ?? (static () =>
                    ComponentTree.Element(
                        "div",
                        children: [ComponentTree.Text("content")]));
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            CssVariables.UseCssVariables(context, _getter);
            return () =>
            {
                _onRender?.Invoke();
                return _render();
            };
        }
    }

    private sealed class PlainTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static () => ComponentTree.Element("div");
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return null;
        }
    }
}
