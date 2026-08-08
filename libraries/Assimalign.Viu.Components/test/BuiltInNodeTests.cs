using System;
using System.Collections.Generic;
using System.Diagnostics;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class BuiltInNodeTests
{
    [Fact]
    public void ElementNode_DebuggerDisplay_DescribesStoredStructure()
    {
        var attribute = (DebuggerDisplayAttribute?)Attribute.GetCustomAttribute(
            typeof(ElementNode),
            typeof(DebuggerDisplayAttribute));

        attribute.ShouldNotBeNull();
        attribute.Value.ShouldBe(
            "<{Name,nq}> Bindings = {Bindings.Count}, Children = {Children.Count}, Directives = {Directives.Count}");
    }

    [Fact]
    public void SuspenseNode_InvocationSlots_StayUnevaluatedAtConstruction()
    {
        // The built-in carries its content through the invocation's lazy default slot; describing
        // the node must never evaluate the slot delegate.
        var invoked = false;
        var invocation = new ComponentInvocation(
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ =>
                {
                    invoked = true;
                    return new TextNode("content");
                },
            });

        var suspense = new SuspenseNode(invocation);

        suspense.Kind.ShouldBe(VirtualNodeKind.Suspense);
        invoked.ShouldBeFalse();
        suspense.Invocation.Slots.ContainsKey("default").ShouldBeTrue();
    }
}
