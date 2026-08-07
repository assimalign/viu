using System;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Components.Tests;

public sealed class VirtualNodeContractTests
{
    [Fact]
    public void ElementNode_QualifiedNameAndChildren_PreserveStructuralShape()
    {
        var child = new TextNode("value");
        var element = new ElementNode(
            new QualifiedName("item", "urn:example:document", "example"),
            children: new[] { child });

        element.Kind.ShouldBe(VirtualNodeKind.Element);
        element.Name.LocalName.ShouldBe("item");
        element.Name.NamespaceName.ShouldBe("urn:example:document");
        element.Children.ShouldBe(new VirtualNode[] { child });
    }

    [Fact]
    public void RenderPlan_NullAndEmptyDynamicChildren_PreserveDifferentBlockMeanings()
    {
        var ordinaryPlan = new RenderPlan();
        var staticBlockPlan = new RenderPlan(dynamicChildren: Array.Empty<VirtualNode>());

        ordinaryPlan.IsBlock.ShouldBeFalse();
        ordinaryPlan.DynamicChildren.ShouldBeNull();
        staticBlockPlan.IsBlock.ShouldBeTrue();
        staticBlockPlan.DynamicChildren.ShouldNotBeNull();
        staticBlockPlan.DynamicChildren.Count.ShouldBe(0);
    }
}
