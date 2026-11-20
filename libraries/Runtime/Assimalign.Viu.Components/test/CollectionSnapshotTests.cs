using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Components.Tests;

/// <summary>
/// Pins the single-copy snapshot contract: a collection handed to a node, plan, invocation, or
/// bindings constructor is copied once into an immutable snapshot, and a snapshot handed back in
/// is adopted without another copy. [CMP-2], [V01.01.11.07]
/// </summary>
public sealed class CollectionSnapshotTests
{
    [Fact]
    public void CloseBlock_Snapshot_IsAdoptedByRenderPlanWithoutCopying()
    {
        ComponentRenderFrame frame = new();
        TextNode tracked = new("tracked");
        frame.OpenBlock();
        frame.Track(tracked);
        IReadOnlyList<VirtualNode>? block = frame.CloseBlock();

        RenderPlan plan = new(PatchFlags.NeedPatch, dynamicChildren: block);

        plan.DynamicChildren.ShouldBeSameAs(block);
        plan.DynamicChildren.ShouldHaveSingleItem().ShouldBeSameAs(tracked);
    }

    [Fact]
    public void ElementNode_ChildrenSnapshotPassedToAnotherNode_IsAdoptedWithoutCopying()
    {
        ElementNode first = new(new QualifiedName("div"), children: [new TextNode("a"), new TextNode("b")]);

        ElementNode second = new(new QualifiedName("section"), children: first.Children);
        RenderPlan plan = new(PatchFlags.NeedPatch, dynamicChildren: first.Children);

        second.Children.ShouldBeSameAs(first.Children);
        plan.DynamicChildren.ShouldBeSameAs(first.Children);
    }

    [Fact]
    public void ElementNode_MutableSourceCollection_IsCopiedOnceAndStaysReadOnly()
    {
        List<VirtualNode> children = [new TextNode("a")];
        ElementNode element = new(new QualifiedName("div"), children: children);
        children.Add(new TextNode("later"));

        element.Children.Count.ShouldBe(1);
        IList<VirtualNode> list = element.Children.ShouldBeAssignableTo<IList<VirtualNode>>()!;
        list.IsReadOnly.ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => list.Add(new TextNode("x")));
        Should.Throw<NotSupportedException>(() => list.RemoveAt(0));
        Should.Throw<NotSupportedException>(() => list[0] = new TextNode("x"));
        list.IndexOf(children[0]).ShouldBe(0);
        list.Contains(children[1]).ShouldBeFalse();
    }

    [Fact]
    public void ElementNode_NullChild_ThrowsNamingTheParameter()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new ElementNode(new QualifiedName("div"), children: [new TextNode("a"), null!]));

        exception.ParamName.ShouldBe("children");
        Should.Throw<ArgumentException>(
                () => new RenderPlan(PatchFlags.NeedPatch, dynamicChildren: [null!]))
            .ParamName.ShouldBe("dynamicChildren");
    }

    [Fact]
    public void ComponentInvocation_EmptyArguments_ShareOneReadOnlySnapshot()
    {
        ComponentInvocation invocation = new(arguments: new Dictionary<string, object?>());

        invocation.Arguments.ShouldBeSameAs(ComponentInvocation.Empty.Arguments);
        IDictionary<string, object?> dictionary =
            invocation.Arguments.ShouldBeAssignableTo<IDictionary<string, object?>>()!;
        dictionary.IsReadOnly.ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => dictionary["later"] = 1);
        Should.Throw<NotSupportedException>(() => dictionary.Remove("later"));
    }

    [Fact]
    public void ComponentInvocation_MutableArguments_AreCopiedOnceAndAdoptedByBindings()
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal) { ["title"] = "first" };
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal) { ["default"] = static _ => null };
        ComponentInvocation invocation = new(arguments, slots);
        arguments["title"] = "later";
        slots.Clear();
        ComponentContract contract = new(parameters: [new ComponentParameter("title")]);

        ComponentBindings bindings = ComponentBindings.Resolve(contract, invocation);

        invocation.Arguments["title"].ShouldBe("first");
        invocation.Slots.Count.ShouldBe(1);
        bindings.Slots.ShouldBeSameAs(invocation.Slots);
        bindings.Parameters["title"].ShouldBe("first");
        ComponentBindings rewrapped = new(bindings.Parameters, bindings.Slots, bindings.FallthroughBindings);
        rewrapped.Parameters.ShouldBeSameAs(bindings.Parameters);
        rewrapped.FallthroughBindings.ShouldBeSameAs(bindings.FallthroughBindings);
    }

    [Fact]
    public void Resolve_RepeatedResolution_SharesAliasTablesAndReplaysDeclarationDiagnostics()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("model-value"), new ComponentParameter("modelValue")]);
        List<ComponentBindingDiagnostic> first = [];
        List<ComponentBindingDiagnostic> second = [];

        ComponentBindings.Resolve(contract, ComponentInvocation.Empty, first);
        ComponentBindings.Resolve(contract, ComponentInvocation.Empty, second);

        contract.Aliases.ShouldBeSameAs(contract.Aliases);
        first.Count.ShouldBe(2);
        second.Count.ShouldBe(2);
        first.ShouldAllBe(diagnostic => diagnostic.Kind == ComponentBindingDiagnosticKind.DuplicateAlias);
        second.ShouldAllBe(diagnostic => diagnostic.Kind == ComponentBindingDiagnosticKind.DuplicateAlias);
        for (int index = 0; index < first.Count; index++)
        {
            second[index].Name.ShouldBe(first[index].Name);
            second[index].Message.ShouldBe(first[index].Message);
        }
    }

    [Fact]
    public void Resolve_ContractWithoutDuplicateAliases_ReportsNothingAcrossRepeatedResolutions()
    {
        ComponentContract contract = new(
            parameters: [new ComponentParameter("modelValue")],
            events: [new ComponentEvent("save")]);
        List<ComponentBindingDiagnostic> diagnostics = [];

        ComponentBindings.Resolve(contract, ComponentInvocation.Empty, diagnostics);
        ComponentBindings.Resolve(contract, ComponentInvocation.Empty, diagnostics);

        diagnostics.ShouldBeEmpty();
        contract.Aliases.Parameters.Keys.ShouldBe(["modelValue", "model-value"], ignoreOrder: true);
        contract.Aliases.Events.Keys.ShouldBe(["save"]);
    }
}
