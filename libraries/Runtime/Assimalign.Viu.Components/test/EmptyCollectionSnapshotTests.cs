using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Components.Tests;

/// <summary>Pins immutable empty snapshots across the allocation optimization. [CMP-2], [V01.01.11.07]</summary>
public class EmptyCollectionSnapshotTests
{
    [Fact]
    public void Element_InitiallyEmptyMutableInputs_RemainEmptyAfterCallerMutation()
    {
        List<ElementBinding> bindings = [];
        List<VirtualNode> children = [];
        ElementNode first = new(new QualifiedName("div"), bindings, children);

        bindings.Add(ElementBinding.Attribute(new QualifiedName("title"), "later"));
        children.Add(new TextNode("later"));
        ElementNode second = new(new QualifiedName("div"), bindings, children);

        first.Bindings.ShouldBeEmpty();
        first.Children.ShouldBeEmpty();
        second.Bindings.Count.ShouldBe(1);
        second.Children.Count.ShouldBe(1);
    }

    [Fact]
    public void Invocation_InitiallyEmptyMutableArguments_RemainIndependentAcrossInstances()
    {
        Dictionary<string, object?> arguments = [];
        ComponentInvocation first = new(arguments);

        arguments.Add("title", "later");
        ComponentInvocation second = new(arguments);

        first.Arguments.ShouldBeEmpty();
        second.Arguments["title"].ShouldBe("later");
        new ComponentInvocation().Arguments.ShouldBeEmpty();
    }
}
