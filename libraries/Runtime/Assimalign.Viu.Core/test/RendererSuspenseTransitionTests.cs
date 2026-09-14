using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>Pins Suspense reveal choreography with host-neutral transitions [BLT-13].</summary>
public sealed class RendererSuspenseTransitionTests
{
    [Fact]
    public void Resolve_FallbackHasAsynchronousLeave_KeepsFallbackUntilLeaveCompletes()
    {
        using RendererParityHost host = new();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<TransitionWrapper>(_ => load.Task);
        List<string> events = [];
        Action? completeLeave = null;
        TransitionNode fallback = Transition(
            new ElementNode(new QualifiedName("aside"), children: [new TextNode("waiting")]),
            new TransitionProperties
            {
                OnBeforeLeave = _ => events.Add("before-leave"),
                OnLeave = (_, complete) =>
                {
                    events.Add("leave");
                    completeLeave = complete;
                },
                OnAfterLeave = _ => events.Add("after-leave"),
            });
        SuspenseNode root = Boundary(definition.CreateComponent(), fallback, events);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, Application(root, definition));
        RendererParityNode fallbackElement = host.Container.Children.Single(node => node.Kind == RendererParityNodeKind.Element);

        load.SetResult(AsynchronousComponentTarget.From<TransitionTarget>());
        host.RunUntilIdle();

        // [BLT-13]: reveal and resolve are held behind the outgoing fallback's leave callback.
        VisibleText(host.Container).ShouldBe("waiting");
        fallbackElement.Parent.ShouldBeSameAs(host.Container);
        events.ShouldBe(["pending", "fallback", "before-leave", "leave"]);

        completeLeave.ShouldNotBeNull()();
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("resolved");
        fallbackElement.Parent.ShouldBeNull();
        // [BLT-13]: leave completion reveals content before the transition's after-leave notification.
        events.ShouldBe(["pending", "fallback", "before-leave", "leave", "resolve", "after-leave"]);
        completeLeave();
        host.RunUntilIdle();
        events.Count(value => value == "resolve").ShouldBe(1);
        renderer.Render(null, host.Container);
    }

    [Fact]
    public void Mount_ContentHasAppearTransition_DefersEnterHooksUntilReveal()
    {
        using RendererParityHost host = new();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = AsynchronousComponents.Define<TransitionWrapper>(_ => load.Task);
        List<string> events = [];
        List<string> transitionCalls = [];
        TransitionNode content = Transition(
            new ElementNode(new QualifiedName("main"), children: [definition.CreateComponent()]),
            new TransitionProperties
            {
                Appear = true,
                OnBeforeAppear = _ =>
                {
                    transitionCalls.Add("before-appear");
                    VisibleText(host.Container).ShouldBe("resolved");
                },
                OnAppear = (_, complete) =>
                {
                    transitionCalls.Add("appear");
                    complete();
                },
                OnAfterAppear = _ => transitionCalls.Add("after-appear"),
            });
        SuspenseNode root = Boundary(content, new TextNode("waiting"), events);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, Application(root, definition));

        // [BLT-13], [BLT-18]: detached content starts no host-visible enter/appear phase.
        transitionCalls.ShouldBeEmpty();
        VisibleText(host.Container).ShouldBe("waiting");
        load.SetResult(AsynchronousComponentTarget.From<TransitionTarget>());
        host.RunUntilIdle();

        VisibleText(host.Container).ShouldBe("resolved");
        transitionCalls.ShouldBe(["before-appear", "appear", "after-appear"]);
        events.ShouldBe(["pending", "fallback", "resolve"]);
        renderer.Render(null, host.Container);
    }

    private static TransitionNode Transition(VirtualNode content, TransitionProperties properties) =>
        new(new ComponentInvocation(
            arguments: new Dictionary<string, object?> { [TransitionProperties.ResolvedArgument] = properties },
            slots: new Dictionary<string, ComponentSlot> { ["default"] = _ => content }));

    private static SuspenseNode Boundary(VirtualNode content, VirtualNode fallback, List<string> events) =>
        new(new ComponentInvocation(
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => content,
                ["fallback"] = _ => fallback,
            },
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["pending"] = _ => events.Add("pending"),
                ["fallback"] = _ => events.Add("fallback"),
                ["resolve"] = _ => events.Add("resolve"),
            }));

    private static ApplicationContext Application(VirtualNode root, AsynchronousComponentDefinition definition)
    {
        ComponentFactory components = new();
        components.Register(definition.Registration);
        components.Register(new ComponentRegistration(
            ComponentReference.ForType(typeof(TransitionTarget)),
            new ComponentContract(),
            _ => new TransitionTarget()));
        return new(new ApplicationOptions { RootComponent = root, Components = components });
    }

    private static string VisibleText(RendererParityNode node) =>
        node.Kind == RendererParityNodeKind.Text ? node.Text ?? string.Empty : string.Concat(node.Children.Select(VisibleText));

    private sealed class TransitionWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => throw new NotSupportedException();
    }

    private sealed class TransitionTarget : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new ElementNode(new QualifiedName("strong"), children: [new TextNode("resolved")]);
    }
}
