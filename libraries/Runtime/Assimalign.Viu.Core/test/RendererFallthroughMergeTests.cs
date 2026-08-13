using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererFallthroughMergeTests
{
    [Fact]
    public void Render_ClassAndStyleFallthrough_NormalizesNestedValuesWithParentPrecedence()
    {
        using var host = new RendererParityHost();
        ElementBinding[] rootBindings =
        [
            ElementBinding.Attribute(
                new QualifiedName("class"),
                new object?[]
                {
                    " root ",
                    new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["suppressed"] = false,
                    },
                    new object?[] { "nested" },
                }),
            ElementBinding.Attribute(
                new QualifiedName("style"),
                new object?[]
                {
                    new Dictionary<string, object?>
                    {
                        ["color"] = "red",
                        ["padding"] = "1px",
                    },
                    "border-width: 1px",
                }),
        ];
        var parentArguments = new Dictionary<string, object?>
        {
            ["class"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["selected"] = 1,
                    ["ignored"] = 0,
                },
                " parent ",
            },
            ["style"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["color"] = "blue",
                    ["margin"] = "2px",
                },
                "padding: 3px",
            },
        };

        RendererParityNode element = RenderComponent(
            host,
            rootBindings,
            parentArguments);

        // [CMP-17] preserves root-first class order while recursively normalizing list and
        // dictionary forms.
        element.Bindings["class"].ShouldBe("root enabled nested selected parent");
        IReadOnlyDictionary<string, object?> style = element.Bindings["style"]
            .ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        style.Count.ShouldBe(4);
        style["color"].ShouldBe("blue");
        style["padding"].ShouldBe("3px");
        style["border-width"].ShouldBe("1px");
        style["margin"].ShouldBe("2px");
    }

    [Fact]
    public void Render_CompatibleEventFallthrough_InvokesRootThenParentAndReplacesOtherValues()
    {
        using var host = new RendererParityHost();
        List<string> invocationOrder = [];
        Action rootClick = () => invocationOrder.Add("root");
        Action parentClick = () => invocationOrder.Add("parent");
        Action rootChange = static () => { };
        Action<object?> parentChange = static _ => { };
        ElementBinding[] rootBindings =
        [
            ElementBinding.Event("click", rootClick),
            ElementBinding.Event("change", rootChange),
            ElementBinding.Attribute(new QualifiedName("title"), "root title"),
        ];
        var parentArguments = new Dictionary<string, object?>
        {
            ["onClick"] = ElementBinding.Event("click", parentClick),
            ["onChange"] = ElementBinding.Event("change", parentChange),
            ["title"] = "parent title",
        };

        RendererParityNode element = RenderComponent(
            host,
            rootBindings,
            parentArguments);
        Action combinedClick = element.Bindings["click"].ShouldBeAssignableTo<Action>()!;

        combinedClick();

        // [CMP-17] combines compatible listeners in root-then-parent order.
        invocationOrder.ShouldBe(["root", "parent"]);
        element.Bindings["change"].ShouldBeSameAs(parentChange);
        element.Bindings["title"].ShouldBe("parent title");
    }

    [Fact]
    public void Render_FallthroughUpdate_RefreshesContextBeforeLifecycleAndPatchesSameRoot()
    {
        using var host = new RendererParityHost();
        List<string> order = [];
        ComponentReference reference = ComponentReference.ForType(
            typeof(FallthroughUpdateComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new FallthroughUpdateComponent(order)));
        ComponentNode initial = Request(reference, "first");
        ApplicationContext application = new(
            new ApplicationOptions
            {
                RootComponent = initial,
                Components = components,
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, application);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        order.Clear();

        renderer.Render(Request(reference, "second"), host.Container);

        host.Container.Children.ShouldHaveSingleItem().ShouldBeSameAs(element);
        element.Bindings["title"].ShouldBe("second");
        order.ShouldBe(
        [
            "before-update:second",
            "render:second",
            "updated:second",
        ]);
    }

    [Fact]
    public void Render_NonElementFallthrough_WarnsAfterInitialBindingDiagnosticsAndOnUpdate()
    {
        using var host = new RendererParityHost();
        List<string> warnings = [];
        ComponentReference reference = ComponentReference.ForType(
            typeof(NonElementRootComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    parameters: [new ComponentParameter("required", isRequired: true)]),
                _ => new NonElementRootComponent()));
        ComponentNode initial = Request(reference, "first");
        ApplicationContext application = new(
            new ApplicationOptions
            {
                RootComponent = initial,
                Components = components,
                WarnHandler = warnings.Add,
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, application);

        warnings.Count.ShouldBe(2);
        warnings[0].ShouldContain("Required parameter 'required'");
        warnings[1].ShouldBe(
            "Component root bindings and directives require one element root.");

        renderer.Render(Request(reference, "second"), host.Container);

        warnings.Count.ShouldBe(3);
        warnings[2].ShouldBe(
            "Component root bindings and directives require one element root.");
    }

    private static ComponentNode Request(ComponentReference reference, string title) =>
        new(
            reference,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["title"] = title }));

    private static RendererParityNode RenderComponent(
        RendererParityHost host,
        IEnumerable<ElementBinding> rootBindings,
        IReadOnlyDictionary<string, object?> parentArguments)
    {
        ComponentRegistration registration = ComponentRegistration.Define(
            "fallthrough-merge-test",
            new ComponentContract(),
            _ => _ => new ElementNode(
                new QualifiedName("fallthrough-root"),
                bindings: rootBindings));
        var components = new ComponentFactory();
        components.Register(registration);
        var request = new ComponentNode(
            registration.Reference,
            new ComponentInvocation(arguments: parentArguments));
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = request,
                Components = components,
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(request, host.Container, application);

        return host.Container.Children.ShouldHaveSingleItem();
    }

    private sealed class FallthroughUpdateComponent : IComponent
    {
        private readonly List<string> _order;

        internal FallthroughUpdateComponent(List<string> order)
        {
            _order = order;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnBeforeUpdate(
                () => _order.Add(
                    $"before-update:{context.Bindings.FallthroughBindings["title"]}"));
            context.Lifecycle.OnUpdated(
                () => _order.Add(
                    $"updated:{context.Bindings.FallthroughBindings["title"]}"));
            return _ =>
            {
                _order.Add(
                    $"render:{context.Bindings.FallthroughBindings["title"]}");
                return new ElementNode(new QualifiedName("fallthrough-update-root"));
            };
        }
    }

    private sealed class NonElementRootComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new FragmentNode([new TextNode("content")]);
    }
}
