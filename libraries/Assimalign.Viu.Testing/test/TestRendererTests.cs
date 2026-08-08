using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestRendererTests
{
    [Fact]
    public void Render_InitialTree_CommitsExactlyOnce()
    {
        Scheduler.Reset();
        TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        ElementNode tree = Element("root", new TextNode("hello"));

        renderer.Render(tree, container);

        TestNodeSerializer.Serialize(container).ShouldBe("<root><root>hello</root></root>");
        renderer.OperationLog.Count(TestNodeOperationType.Commit).ShouldBe(1);
        Scheduler.Reset();
    }

    [Fact]
    public void Render_TextPatch_ChangesOnlyTextAndCommitsOnce()
    {
        Scheduler.Reset();
        TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(Element("root", new TextNode("before")), container);
        renderer.OperationLog.Reset();

        renderer.Render(Element("root", new TextNode("after")), container);

        renderer.OperationLog.Count(TestNodeOperationType.SetText).ShouldBe(1);
        renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
        renderer.OperationLog.Count(TestNodeOperationType.Commit).ShouldBe(1);
        Scheduler.Reset();
    }

    [Fact]
    public void SchedulerFlush_ReactiveRender_CoalescesWritesIntoOneCommit()
    {
        Scheduler.Reset();
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ReactiveTextComponent component = new();
        ComponentReference reference = ComponentReference.ForType(typeof(ReactiveTextComponent));
        ComponentFactory factory = new();
        factory.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(displayName: "ReactiveText"),
                _ => component));
        ComponentNode root = new(reference);
        ApplicationContext application = new(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = factory,
            });
        TestRenderer renderer = new();
        TestElement container = renderer.CreateContainer();
        renderer.Render(root, container, application);
        pump.RunUntilIdle();
        renderer.OperationLog.Reset();

        component.Value.Value = 1;
        component.Value.Value = 2;
        pump.PendingFlushCount.ShouldBe(1);
        pump.RunUntilIdle();

        renderer.OperationLog.Count(TestNodeOperationType.SetText).ShouldBe(1);
        renderer.OperationLog.Count(TestNodeOperationType.Commit).ShouldBe(1);
        TestNodeSerializer.Serialize(container).ShouldContain(">2<");
        renderer.Render(null, container);
        Scheduler.Reset();
    }

    [Fact]
    public void Hydrate_MatchingServerElement_AdoptsIdentityWithoutCreation()
    {
        Scheduler.Reset();
        TestElement container = TestServerMarkup.Parse("<main data-id=server>hello</main>");
        TestElement serverElement = (TestElement)container.Children.ShouldHaveSingleItem();
        TestRenderer renderer = new(snapshotSemantics: true);
        ElementNode client = new(
            new QualifiedName("main"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("data-id"), "server"),
            ],
            children: new[] { new TextNode("hello") });

        renderer.Hydrate(client, container);

        container.Children.ShouldHaveSingleItem().ShouldBeSameAs(serverElement);
        renderer.OperationLog.Count(TestNodeOperationType.CreateElement).ShouldBe(0);
        renderer.OperationLog.Count(TestNodeOperationType.CreateText).ShouldBe(0);
        renderer.OperationLog.Count(TestNodeOperationType.Commit).ShouldBe(1);
        Scheduler.Reset();
    }

    [Fact]
    public void FrozenHydrationReader_HostMutation_PreservesCapturedTopology()
    {
        TestElement root = TestServerMarkup.Parse("<a>first</a><b>second</b>");
        TestNode first = root.Children[0];
        TestNode second = root.Children[1];
        FrozenTestHydrationReader reader = new(root);
        RendererOptions<TestNode> operations = TestNodeOperations.Create(
            new TestNodeOperationLog());
        operations.Remove(second);

        reader.FirstChild(root).ShouldBeSameAs(first);
        reader.NextSibling(first).ShouldBeSameAs(second);
        reader.ParentNode(second).ShouldBeSameAs(root);
    }

    private static ElementNode Element(string name, params VirtualNode[] children) =>
        new(new QualifiedName(name), children: children);

    private sealed class ReactiveTextComponent : IComponent
    {
        internal Reference<int> Value { get; } = new(0);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => Element("value", new TextNode(Value.Value.ToString()));
        }
    }
}
