using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins the host-neutral child snapshot and shared transition state consumed by host-specific
/// TransitionGroup implementations.
/// </summary>
public sealed class TransitionGroupCoreTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public TransitionGroupCoreTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void GetKeyedChildElements_LifecyclePhasesExposeOutgoingAndIncomingElementChildren()
    {
        Reference<IReadOnlyList<string>> keys =
            Reactive.Reference<IReadOnlyList<string>>(["a", "b"]);
        SnapshotLifecycleTemplate template = new(keys);
        ITemplateComponent root =
            ComponentTree.Template<SnapshotLifecycleTemplate>();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            Application(
                root,
                new ComponentRegistration(
                    typeof(SnapshotLifecycleTemplate),
                    () => template)));
        _pump.RunUntilIdle();
        FakeHostNode wrapper = host.Root.Children.Single();
        FakeHostNode firstA = wrapper.Children[0];
        FakeHostNode firstB = wrapper.Children[1];

        keys.Value = ["b", "c"];
        _pump.RunUntilIdle();

        template.Outgoing.Select(item => item.Key)
            .ShouldBe(["a", "b"]);
        template.Outgoing.Select(item => item.Element)
            .ShouldBe([firstA, firstB]);
        template.Incoming.Select(item => item.Key)
            .ShouldBe(["b", "c"]);
        template.Incoming[0].Element.ShouldBeSameAs(firstB);
        template.Incoming[1].Element.ShouldBeSameAs(
            wrapper.Children[1]);
        template.Incoming.Select(item => item.Component.Key)
            .ShouldBe(["b", "c"]);
    }

    [Fact]
    public void GetKeyedChildElements_FragmentRootDescendsTemplateAndFragmentWrappers()
    {
        SnapshotFragmentTemplate template = new();
        ITemplateComponent root =
            ComponentTree.Template<SnapshotFragmentTemplate>();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IComponentContext context = renderer.Render(
            root,
            host.Root,
            Application(
                root,
                new ComponentRegistration(
                    typeof(SnapshotFragmentTemplate),
                    () => template),
                new ComponentRegistration(
                    typeof(SnapshotItemTemplate),
                    static () => new SnapshotItemTemplate())))!;
        _pump.RunUntilIdle();

        IReadOnlyList<KeyedComponentHostElement<FakeHostNode>> snapshots =
            ComponentHost.GetKeyedChildElements<FakeHostNode>(context);

        snapshots.Select(item => item.Key)
            .ShouldBe(["template", "fragment"]);
        snapshots.Select(item => item.Element.Content)
            .ShouldBe(["span", "strong"]);
        snapshots.ShouldAllBe(
            item => item.Element.Kind == FakeHostNodeKind.Element);
        snapshots[0].Component
            .ShouldBeAssignableTo<ITemplateComponent>();
        snapshots[1].Component
            .ShouldBeAssignableTo<IFragmentComponent>();
    }

    [Fact]
    public void ComponentTransitionScope_AttachAndFinishPendingEnter_ShareLifecycleState()
    {
        Reference<bool> showSecond = Reactive.Reference(false);
        TransitionScopeTemplate template = new(showSecond);
        ITemplateComponent root =
            ComponentTree.Template<TransitionScopeTemplate>();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            Application(
                root,
                new ComponentRegistration(
                    typeof(TransitionScopeTemplate),
                    () => template)));
        _pump.RunUntilIdle();

        template.EnterCalls.ShouldBe(0);
        showSecond.Value = true;
        _pump.RunUntilIdle();

        template.EnterCalls.ShouldBe(1);
        template.AfterEnterCalls.ShouldBe(0);
        template.PendingElement.ShouldNotBeNull();
        template.Scope!.FinishPendingEnter(
                template.PendingElement!)
            .ShouldBeTrue();
        template.AfterEnterCalls.ShouldBe(1);
        template.Scope.FinishPendingEnter(
                template.PendingElement!)
            .ShouldBeFalse();

        renderer.Render(null, host.Root);
        _pump.RunUntilIdle();

        host.Root.Children.ShouldBeEmpty();
        template.LeaveCalls.ShouldBe(0);
    }

    private static IApplicationContext Application(
        IComponent root,
        params ComponentRegistration[] registrations)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceProvider());
    }

    private static IReadOnlyList<IComponent> KeyedElements(
        IReadOnlyList<string> keys)
    {
        List<IComponent> children = new(keys.Count);
        for (int index = 0; index < keys.Count; index++)
        {
            string key = keys[index];
            children.Add(
                ComponentTree.Element(
                    "li",
                    children: [ComponentTree.Text(key)],
                    key: key));
        }

        return children;
    }

    private sealed class SnapshotLifecycleTemplate : IComponentTemplate
    {
        private readonly Reference<IReadOnlyList<string>> _keys;

        internal SnapshotLifecycleTemplate(
            Reference<IReadOnlyList<string>> keys)
        {
            _keys = keys;
        }

        internal IReadOnlyList<
            KeyedComponentHostElement<FakeHostNode>> Outgoing { get; private set; } =
            [];

        internal IReadOnlyList<
            KeyedComponentHostElement<FakeHostNode>> Incoming { get; private set; } =
            [];

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnBeforeUpdate(
                () => Outgoing =
                    ComponentHost.GetKeyedChildElements<FakeHostNode>(
                        context));
            context.Lifecycle.OnUpdated(
                () => Incoming =
                    ComponentHost.GetKeyedChildElements<FakeHostNode>(
                        context));
            return () => ComponentTree.Element(
                "ul",
                children: KeyedElements(_keys.Value));
        }
    }

    private sealed class SnapshotFragmentTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Fragment(
            [
                ComponentTree.Template<SnapshotItemTemplate>(
                    key: "template"),
                ComponentTree.Fragment(
                [
                    ComponentTree.Comment("anchor"),
                    ComponentTree.Element("strong"),
                ],
                    key: "fragment"),
                ComponentTree.Static(
                    "static",
                    key: "static"),
            ]);
        }
    }

    private sealed class SnapshotItemTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element("span");
        }
    }

    private sealed class TransitionScopeTemplate : IComponentTemplate
    {
        private readonly Reference<bool> _showSecond;

        internal TransitionScopeTemplate(
            Reference<bool> showSecond)
        {
            _showSecond = showSecond;
        }

        internal ComponentTransitionScope? Scope { get; private set; }

        internal object? PendingElement { get; private set; }

        internal int EnterCalls { get; private set; }

        internal int AfterEnterCalls { get; private set; }

        internal int LeaveCalls { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            Scope = new ComponentTransitionScope(context);
            BaseTransitionProperties properties =
                new()
                {
                    OnEnter = (element, _) =>
                    {
                        EnterCalls++;
                        PendingElement = element;
                    },
                    OnAfterEnter = _ => AfterEnterCalls++,
                    OnLeave = (_, _) => LeaveCalls++,
                };
            return () =>
            {
                List<IComponent> children =
                [
                    Scope.Attach(
                        ComponentTree.Element(
                            "span",
                            key: "a"),
                        properties),
                ];
                if (_showSecond.Value)
                {
                    children.Add(
                        Scope.Attach(
                            ComponentTree.Element(
                                "span",
                                key: "b"),
                            properties));
                }

                return ComponentTree.Element(
                    "div",
                    children: children);
            };
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
