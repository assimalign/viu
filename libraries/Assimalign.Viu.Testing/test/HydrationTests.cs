using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class HydrationTests
{
    [Fact]
    public void Hydrate_MatchingElement_AdoptsServerTreeWithoutHostOperations()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div id=\"application\"><span>ready</span></div>");
        TestNode serverElement = container.Children[0];
        TestNode serverChild = ((TestElement)serverElement).Children[0];
        IComponent component = ComponentTree.Element(
            "div",
            Attributes(("id", "application")),
            [
                ComponentTree.Element(
                    "span",
                    children: [ComponentTree.Text("ready")]),
            ]);

        renderer.Hydrate(component, container);

        container.Children[0].ShouldBeSameAs(serverElement);
        ((TestElement)container.Children[0]).Children[0]
            .ShouldBeSameAs(serverChild);
        renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_EventListener_AttachesToAdoptedElement()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<button>Click</button>");
        TestElement serverButton = (TestElement)container.Children[0];
        int clickCount = 0;
        IComponent component = ComponentTree.Element(
            "button",
            Attributes(("onClick", (Action)(() => clickCount++))),
            [ComponentTree.Text("Click")],
            optimization: new ComponentOptimization(PatchFlags.NeedHydration));

        renderer.Hydrate(component, container);

        container.Children[0].ShouldBeSameAs(serverButton);
        renderer.OperationLog.Count(TestNodeOperationType.PatchAttribute)
            .ShouldBe(1);
        renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        TestEventDispatcher.Trigger(serverButton, "click").ShouldBeTrue();
        clickCount.ShouldBe(1);
    }

    [Fact]
    public void Hydrate_TextMismatch_CorrectsAdoptedTextNode()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div>server</div>");
        TestElement serverElement = (TestElement)container.Children[0];
        TestNode serverText = serverElement.Children[0];
        IComponent component = ComponentTree.Element(
            "div",
            children: [ComponentTree.Text("client")]);
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        container.Children[0].ShouldBeSameAs(serverElement);
        serverElement.Children[0].ShouldBeSameAs(serverText);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>client</div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.SetText)
            .ShouldBe(1);
        renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        warnings.ShouldContain(
            warning => warning.Contains(
                "Hydration text mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Hydrate_StructuralMismatch_ReplacesOnlyMismatchedChild()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div><span>x</span></div>");
        TestElement serverElement = (TestElement)container.Children[0];
        TestNode serverChild = serverElement.Children[0];
        IComponent component = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Element(
                    "p",
                    children: [ComponentTree.Text("x")]),
            ]);

        renderer.Hydrate(component, container);

        container.Children[0].ShouldBeSameAs(serverElement);
        serverElement.Children.ShouldNotContain(serverChild);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div><p>x</p></div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.Remove)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.CreateElement)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.Insert)
            .ShouldBe(2);
    }

    [Fact]
    public void Hydrate_Fragment_AdoptsAnchorsAndChildren()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<!--[--><span>a</span><span>b</span><!--]-->");
        TestNode startAnchor = container.Children[0];
        TestNode firstChild = container.Children[1];
        TestNode secondChild = container.Children[2];
        TestNode endAnchor = container.Children[3];
        IComponent component = ComponentTree.Fragment(
        [
            ComponentTree.Element(
                "span",
                children: [ComponentTree.Text("a")]),
            ComponentTree.Element(
                "span",
                children: [ComponentTree.Text("b")]),
        ],
        optimization: new ComponentOptimization(PatchFlags.StableFragment));

        renderer.Hydrate(component, container);

        container.Children[0].ShouldBeSameAs(startAnchor);
        container.Children[1].ShouldBeSameAs(firstChild);
        container.Children[2].ShouldBeSameAs(secondChild);
        container.Children[3].ShouldBeSameAs(endAnchor);
        renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_ReactiveTemplate_UpdatePatchesAdoptedElement()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div>hello</div>");
        TestNode serverElement = container.Children[0];
        Reference<string> message = Reactive.Reference("hello");
        ReactiveTemplate template = new(message);
        ITemplateComponent component = ComponentTree.Template<ReactiveTemplate>();
        IApplicationContext application = CreateApplication(
            component,
            warnings: null,
            new ComponentRegistration(
                typeof(ReactiveTemplate),
                () => template));

        IComponentContext? context = renderer.Hydrate(
            component,
            container,
            application);
        pump.RunUntilIdle();
        renderer.OperationLog.Reset();

        message.Value = "updated";
        pump.RunUntilIdle();

        context.ShouldNotBeNull();
        container.Children[0].ShouldBeSameAs(serverElement);
        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>updated</div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.SetText)
            .ShouldBe(1);
        renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
    }

    [Fact]
    public void Hydrate_Teleport_AdoptsRegisteredTargetContent()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<!--teleport start--><!--teleport end-->");
        TestElement targetRoot = TestServerMarkup.Parse(
            "<div id=\"modal\"><span>x</span><!--teleport anchor--></div>");
        TestElement target = (TestElement)targetRoot.Children[0];
        TestNode serverTargetChild = target.Children[0];
        renderer.RegisterQueryRoot(targetRoot);
        IComponent component = ComponentTree.Teleport(
            "#modal",
            [
                ComponentTree.Element(
                    "span",
                    children: [ComponentTree.Text("x")]),
            ]);

        renderer.Hydrate(component, container);

        target.Children[0].ShouldBeSameAs(serverTargetChild);
        TestNodeSerializer.Serialize(container).ShouldBe(
            "<root><!--teleport start--><!--teleport end--></root>");
        TestNodeSerializer.Serialize(target).ShouldBe(
            "<div id=\"modal\"><span>x</span><!--teleport anchor--></div>");
        renderer.OperationLog.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_EmptyContainer_PerformsFullClientMount()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(string.Empty);
        IComponent component = ComponentTree.Element(
            "div",
            children: [ComponentTree.Text("client")]);
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>client</div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.CreateElement)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.Insert)
            .ShouldBe(2);
        warnings.ShouldContain(
            warning => warning.Contains(
                "container is empty",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Hydrate_FrozenReader_CollectsFragmentRangeBeforeRemoval()
    {
        TestRenderer renderer = new(snapshotSemantics: true);
        TestElement container = TestServerMarkup.Parse(
            "<!--[--><span>x</span><span>y</span><!--]-->");
        IComponent component = ComponentTree.Element(
            "div",
            children: [ComponentTree.Text("z")]);

        renderer.Hydrate(component, container);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>z</div></root>");
        renderer.OperationLog.Count(TestNodeOperationType.Remove)
            .ShouldBe(4);
        renderer.OperationLog.Count(TestNodeOperationType.CreateElement)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText)
            .ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.Insert)
            .ShouldBe(2);
    }

    [Fact]
    public void Hydrate_FrozenReader_SplitsAdjacentTextWithoutMismatch()
    {
        TestRenderer renderer = new(snapshotSemantics: true);
        TestElement container = TestServerMarkup.Parse("<div>abc</div>");
        IComponent component = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Text("a"),
                ComponentTree.Text("b"),
                ComponentTree.Text("c"),
            ]);
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div>abc</div></root>");
        ((TestElement)container.Children[0]).Children.Count.ShouldBe(3);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText)
            .ShouldBe(2);
        renderer.OperationLog.Count(TestNodeOperationType.Insert)
            .ShouldBe(2);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_CachedElement_AdoptsWithoutInspectingChildren()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div><span>server</span></div>");
        IComponent component = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Element(
                    "span",
                    children: [ComponentTree.Text("client")]),
            ],
            optimization: new ComponentOptimization(PatchFlags.Cached));
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        TestNodeSerializer.Serialize(container)
            .ShouldBe("<root><div><span>server</span></div></root>");
        renderer.OperationLog.Operations.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_ClassAndStyleOrderDifferences_AreEquivalent()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div class=\"b a\" style=\"color:red;display:block\"></div>");
        IComponent component = ComponentTree.Element(
            "div",
            Attributes(
                ("class", "a b"),
                ("style", "display:block;color:red;")));
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        renderer.OperationLog.Operations.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_TextContentOverride_SkipsChildWalkAndPatchesProperty()
    {
        TestRenderer renderer = new();
        TestElement container = TestServerMarkup.Parse(
            "<div><span>server</span></div>");
        IComponent component = ComponentTree.Element(
            "div",
            Attributes(("textContent", "client")));
        List<string> warnings = [];
        IApplicationContext application = CreateApplication(component, warnings);

        renderer.Hydrate(component, container, application);

        renderer.OperationLog.Count(TestNodeOperationType.Remove)
            .ShouldBe(0);
        renderer.OperationLog.Count(TestNodeOperationType.PatchAttribute)
            .ShouldBe(1);
        renderer.OperationLog.OfType(TestNodeOperationType.PatchAttribute)[0]
            .PropertyName
            .ShouldBe("textContent");
        warnings.ShouldBeEmpty();
    }

    private static IApplicationContext CreateApplication(
        IComponent root,
        List<string>? warnings,
        params ComponentRegistration[] registrations)
    {
        ApplicationContext application = new(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceResolver());
        if (warnings is not null)
        {
            application.WarnHandler = warnings.Add;
        }

        return application;
    }

    private static ComponentAttributes Attributes(
        params (string Name, object? Value)[] values)
    {
        List<IComponentAttribute> attributes = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            attributes.Add(
                new ComponentAttribute(
                    values[index].Name,
                    values[index].Value));
        }

        return new ComponentAttributes(attributes);
    }

    private sealed class ReactiveTemplate : IComponentTemplate
    {
        private readonly Reference<string> _message;

        internal ReactiveTemplate(Reference<string> message)
        {
            _message = message;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Element(
                "div",
                children: [ComponentTree.Text(_message.Value)]);
        }
    }

    private sealed class EmptyServiceResolver : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
