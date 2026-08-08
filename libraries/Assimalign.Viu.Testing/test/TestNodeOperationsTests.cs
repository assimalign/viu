using System;
using System.Collections.Generic;
using System.Diagnostics;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestNodeOperationsTests
{
    [Fact]
    public void TestElement_DebuggerDisplay_DescribesBackingHostState()
    {
        var attribute = (DebuggerDisplayAttribute?)Attribute.GetCustomAttribute(
            typeof(TestElement),
            typeof(DebuggerDisplayAttribute));

        attribute.ShouldNotBeNull();
        attribute.Value.ShouldBe(
            "<{Name,nq}> #{Identifier} Properties = {_properties.Count}, Children = {_children.Count}, EventListeners = {_eventListeners.Count}");
    }

    [Fact]
    public void Insert_ChildBeforeAnchor_PreservesHostTopology()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log);
        TestNode parent = options.CreateElement(new QualifiedName("parent"));
        TestNode last = options.CreateText("last");
        TestNode first = options.CreateText("first");
        options.Insert(last, parent, null);

        options.Insert(first, parent, last);

        options.ParentNode(first).ShouldBeSameAs(parent);
        options.NextSibling(first).ShouldBeSameAs(last);
        ((TestElement)parent).Children.ShouldBe(new[] { first, last });
    }

    [Fact]
    public void CreateElement_QualifiedName_PreservesNamespaceAndPrefix()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log);
        QualifiedName name = new("path", "urn:test:svg", "svg");

        TestElement element = (TestElement)options.CreateElement(name);

        element.Name.ShouldBe(name);
        element.Tag.ShouldBe("path");
        element.Namespace.ShouldBe("urn:test:svg");
        log.Operations.ShouldHaveSingleItem().Text.ShouldBe("svg:path");
    }

    [Fact]
    public void PatchAttribute_EventBinding_UpdatesInvokerWithoutStructuralMutation()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log);
        TestElement element = (TestElement)options.CreateElement(new QualifiedName("button"));
        int firstRuns = 0;
        int secondRuns = 0;
        ElementBinding first = ElementBinding.Event("click", (Action)(() => firstRuns++));
        ElementBinding second = ElementBinding.Event("click", (Action)(() => secondRuns++));

        options.PatchAttribute(element, null, first);
        options.PatchAttribute(element, first, second);
        TestEventDispatcher.Trigger(element, "click").ShouldBeTrue();

        firstRuns.ShouldBe(0);
        secondRuns.ShouldBe(1);
        log.Count(TestNodeOperationType.PatchAttribute).ShouldBe(2);
        log.StructuralOperationCount.ShouldBe(0);
    }

    [Fact]
    public void TestElement_PublicCollections_AreReadOnlyLiveViewsOfHostState()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log);
        TestElement element = (TestElement)options.CreateElement(new QualifiedName("button"));
        TestNode child = options.CreateText("label");
        TestNode externalChild = options.CreateText("external");
        Action listener = static () => { };
        IReadOnlyDictionary<string, object?> properties = element.Properties;
        IReadOnlyList<TestNode> children = element.Children;
        IReadOnlyDictionary<string, Delegate> eventListeners = element.EventListeners;

        options.Insert(child, element, null);
        options.PatchAttribute(
            element,
            null,
            ElementBinding.Attribute(new QualifiedName("title"), "action"));
        options.PatchAttribute(element, null, ElementBinding.Event("click", listener));

        properties["title"].ShouldBe("action");
        children.ShouldHaveSingleItem().ShouldBeSameAs(child);
        eventListeners["click"].ShouldBeSameAs(listener);
        Should.Throw<NotSupportedException>(
            () => ((IDictionary<string, object?>)properties).Add("external", true));
        Should.Throw<NotSupportedException>(
            () => ((ICollection<TestNode>)children).Add(externalChild));
        Should.Throw<NotSupportedException>(
            () => ((IDictionary<string, Delegate>)eventListeners).Add("external", listener));
    }

    [Fact]
    public void InsertStaticContent_SingleOperation_PreservesTrustedPayload()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log);
        TestElement parent = (TestElement)options.CreateElement(new QualifiedName("root"));

        (TestNode first, TestNode last) = options.InsertStaticContent!(
            MarkupFormat.Html,
            "<b>trusted</b>",
            parent,
            null);

        first.ShouldBeSameAs(last);
        ((TestText)first).IsStaticContent.ShouldBeTrue();
        parent.Children.ShouldHaveSingleItem().ShouldBeSameAs(first);
        log.Count(TestNodeOperationType.InsertStaticContent).ShouldBe(1);
    }

    [Fact]
    public void Remove_StrictModeDuplicateRemoval_Throws()
    {
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(log, strictRemoval: true);
        TestElement parent = (TestElement)options.CreateElement(new QualifiedName("root"));
        TestNode child = options.CreateText("child");
        options.Insert(child, parent, null);
        options.Remove(child);

        Should.Throw<InvalidOperationException>(() => options.Remove(child))
            .Message.ShouldContain("more than once");
    }

    [Fact]
    public void ResolveTeleportTarget_RegisteredRoots_SearchSupportedSelectors()
    {
        TestElement targetRoot = TestServerMarkup.Parse(
            "<section><aside id=target class=port></aside></section>");
        TestNodeOperationLog log = new();
        RendererOptions<TestNode> options = TestNodeOperations.Create(
            log,
            new List<TestElement> { targetRoot });

        TestNode? resolved = options.ResolveTeleportTarget!("#target");

        resolved.ShouldBeOfType<TestElement>().Tag.ShouldBe("aside");
    }
}
