using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

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

    [Fact]
    public void Constructors_InvalidClosedModelValues_ThrowDescriptiveArgumentExceptions()
    {
        Should.Throw<ArgumentException>(() => ElementBinding.Attribute(default, null));
        Should.Throw<ArgumentOutOfRangeException>(
            () => new StaticNode((MarkupFormat)99, "content"));
        Should.Throw<ArgumentException>(
            () => ComponentReference.ForType(typeof(string)));
        Should.Throw<ArgumentException>(
            () => new ElementNode(
                new QualifiedName("div"),
                children: new VirtualNode[] { null! }));
        Should.Throw<ArgumentException>(
            () => new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot> { ["default"] = null! }));

        // Invalid compiler metadata cannot enter the closed tree model [CMP-3] [RND-BLOCK-1].
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RenderPlan((PatchFlags)(-3)));
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RenderPlan(dynamicBindingIndices: [-1]));
        Should.Throw<ArgumentException>(
            () => new RenderPlan(dynamicChildren: new VirtualNode[] { null! }));
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ComponentContract(flags: (ComponentFlags)2));
        Should.Throw<ArgumentException>(
            () => new ComponentContract(parameters: new ComponentParameter[] { null! }));
        Should.Throw<ArgumentException>(
            () => new ComponentContract(events: new ComponentEvent[] { null! }));
    }
}
