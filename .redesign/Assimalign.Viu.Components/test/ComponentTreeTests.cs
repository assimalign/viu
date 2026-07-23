using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentTreeTests
{
    [Fact]
    public void Tree_AllRenderValuesShareComponentContract()
    {
        IComponent[] values =
        {
            ComponentTree.Element("main"),
            ComponentTree.Template<EmptyTemplate>(),
            ComponentTree.Text("text"),
            ComponentTree.Comment(),
            ComponentTree.Static("<span>static</span>"),
            ComponentTree.Fragment(),
            ComponentTree.Teleport("#target"),
        };

        foreach (IComponent component in values)
        {
            ((int)component.Kind).ShouldBeInRange(
                (int)ComponentKind.Element,
                (int)ComponentKind.Teleport);
        }
    }

    [Fact]
    public void Element_ChildrenAndAttributesAreReadOnlySnapshots()
    {
        List<IComponent> children = new() { ComponentTree.Text("first") };
        List<IComponentAttribute> attributes =
            new() { new ComponentAttribute("role", "main") };

        IElementComponent element = ComponentTree.Element(
            "main",
            new ComponentAttributes(attributes),
            children);
        children.Add(ComponentTree.Text("second"));
        attributes.Add(new ComponentAttribute("hidden", true));

        element.Children.Count.ShouldBe(1);
        element.Attributes.Count.ShouldBe(1);
        element.Attributes.TryGetValue("role", out object? role).ShouldBeTrue();
        role.ShouldBe("main");
    }

    private sealed class EmptyTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => null;
        }
    }
}
