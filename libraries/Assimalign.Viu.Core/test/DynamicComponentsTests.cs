using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class DynamicComponentsTests
{
    [Fact]
    public void DynamicComponent_PlainString_CreatesElementAndPreservesElementInputs()
    {
        ElementBinding binding = ElementBinding.Attribute(new QualifiedName("data-id"), "42");
        TextNode child = new("content");
        RenderPlan renderPlan = new(PatchFlags.Text);

        ElementNode element = DynamicComponents.DynamicComponent(
            "article",
            bindings: [binding],
            children: [child],
            key: "article-key",
            renderPlan: renderPlan).ShouldBeOfType<ElementNode>();

        element.Name.ShouldBe(new QualifiedName("article"));
        element.Bindings.ShouldBe([binding]);
        element.Children.ShouldBe([child]);
        element.Key.ShouldBe("article-key");
        element.RenderPlan.ShouldBeSameAs(renderPlan);
    }

    [Fact]
    public void DynamicComponent_ExplicitComponentSelectors_CreateComponentNodesWithoutProbing()
    {
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?> { ["value"] = 42 });
        ComponentReference reference = ComponentReference.ForType(typeof(TargetComponent));
        AsynchronousComponentDefinition asynchronousDefinition =
            AsynchronousComponents.DefineAsynchronousComponent<WrapperIdentityComponent>(
                _ => Task.FromResult(AsynchronousComponentTarget.From<TargetComponent>()));

        ComponentNode typeNode = DynamicComponents.DynamicComponent(
            typeof(TargetComponent),
            invocation).ShouldBeOfType<ComponentNode>();
        ComponentNode referenceNode = DynamicComponents.DynamicComponent(
            reference,
            invocation).ShouldBeOfType<ComponentNode>();
        ComponentNode nameNode = DynamicComponents.DynamicComponent(
            DynamicComponents.Named("registered-target"),
            invocation).ShouldBeOfType<ComponentNode>();
        ComponentNode asynchronousNode = DynamicComponents.DynamicComponent(
            asynchronousDefinition,
            invocation).ShouldBeOfType<ComponentNode>();

        typeNode.Component.ShouldBe(reference);
        referenceNode.Component.ShouldBe(reference);
        nameNode.Component.ShouldBe(ComponentReference.ForName("registered-target"));
        asynchronousNode.Component.ShouldBe(asynchronousDefinition.Reference);
        typeNode.Invocation.ShouldBeSameAs(invocation);
        referenceNode.Invocation.ShouldBeSameAs(invocation);
        nameNode.Invocation.ShouldBeSameAs(invocation);
        asynchronousNode.Invocation.ShouldBeSameAs(invocation);
    }

    [Fact]
    public void DynamicComponent_ExistingOrUnsupportedValue_PreservesNodeOrCreatesPlaceholder()
    {
        TextNode existing = new("existing");

        DynamicComponents.DynamicComponent(existing).ShouldBeSameAs(existing);
        DynamicComponents.DynamicComponent(new object())
            .ShouldBeOfType<CommentNode>()
            .Text.ShouldBeEmpty();
        DynamicComponents.DynamicComponent(null)
            .ShouldBeOfType<CommentNode>()
            .Text.ShouldBeEmpty();
    }

    private sealed class TargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class WrapperIdentityComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }
}
