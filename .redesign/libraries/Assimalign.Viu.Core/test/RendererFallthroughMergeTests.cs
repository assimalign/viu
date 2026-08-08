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
}
