using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

/// <summary>Pins Core's host-neutral transition choreography from <c>[BLT-7]</c> through <c>[BLT-10]</c>.</summary>
public sealed class TransitionRendererTests
{
    [Fact]
    public void Render_InitialMountWithoutAppear_SkipsEnterHooks()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(
            Transition(
                () => Element("div", "content"),
                recorder.Build()),
            host.Container);
        host.RunScheduledFlushes();

        recorder.Calls.ShouldBeEmpty();
        Elements(host.Container).ShouldHaveSingleItem().Description.ShouldBe("div");
    }

    [Fact]
    public void Render_Appear_UsesAppearHooksAndCompletesOnce()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(
            Transition(
                () => Element("div", "content"),
                recorder.Build(appear: true)),
            host.Container);

        recorder.Calls.ShouldBe(["beforeAppear", "appear"]);
        recorder.CompleteEnter();
        recorder.CompleteEnter();

        recorder.Calls.ShouldBe(["beforeAppear", "appear", "afterAppear"]);
    }

    [Fact]
    public void Render_LeaveAndReenter_DefersRemovalAndRunsEachPhaseOnce()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionProperties properties = recorder.Build();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(() => Element("div", "content"), properties),
            host.Container);
        host.RunScheduledFlushes();
        RendererParityNode outgoing = Elements(host.Container).ShouldHaveSingleItem();

        renderer.Render(
            Transition(static () => new CommentNode("hidden"), properties),
            host.Container);

        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        outgoing.Parent.ShouldBeSameAs(host.Container);
        recorder.CompleteLeave();
        outgoing.Parent.ShouldBeNull();
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);

        renderer.Render(
            Transition(() => Element("div", "content"), properties),
            host.Container);

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
        ]);
        recorder.CompleteEnter();
        recorder.Calls[^1].ShouldBe("afterEnter");
    }

    [Fact]
    public void Render_OutgoingThenIncoming_LeavesBeforeMountingAndEnteringReplacement()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionProperties properties = recorder.Build(mode: "out-in");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(() => Element("div", "first"), properties),
            host.Container);
        host.RunScheduledFlushes();

        renderer.Render(
            Transition(() => Element("span", "second"), properties),
            host.Container);

        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        Elements(host.Container).Select(node => node.Description).ShouldBe(["div"]);

        recorder.CompleteLeave();

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
        ]);
        Elements(host.Container).Select(node => node.Description).ShouldBe(["span"]);
    }

    [Fact]
    public void Render_IncomingThenOutgoing_EntersBeforeStartingLeave()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionProperties properties = recorder.Build(mode: "in-out");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(() => Element("div", "first"), properties),
            host.Container);
        host.RunScheduledFlushes();

        renderer.Render(
            Transition(() => Element("span", "second"), properties),
            host.Container);

        recorder.Calls.ShouldBe(["beforeEnter", "enter"]);
        Elements(host.Container).Select(node => node.Description)
            .ShouldBe(["div", "span"]);

        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(
        [
            "beforeEnter",
            "enter",
            "afterEnter",
            "beforeLeave",
            "leave",
        ]);
        recorder.CompleteLeave();
        recorder.Calls[^1].ShouldBe("afterLeave");
        Elements(host.Container).Select(node => node.Description).ShouldBe(["span"]);
    }

    [Fact]
    public void Render_DefaultMode_StartsEnterAndLeaveWithoutWaiting()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionProperties properties = recorder.Build();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(() => Element("div", "first"), properties),
            host.Container);
        host.RunScheduledFlushes();

        renderer.Render(
            Transition(() => Element("span", "second"), properties),
            host.Container);

        recorder.Calls.ShouldBe(["beforeEnter", "enter", "beforeLeave", "leave"]);
        Elements(host.Container).Select(node => node.Description)
            .ShouldBe(["div", "span"]);
    }

    [Fact]
    public void Render_GroupObservers_ReceiveOutgoingAndPatchedIncomingFirstElements()
    {
        using var host = new RendererParityHost();
        IReadOnlyList<TransitionElementSnapshot>? outgoing = null;
        IReadOnlyList<TransitionElementSnapshot>? incoming = null;
        TransitionProperties properties = new()
        {
            OnBeforeUpdate = value => outgoing = value,
            OnUpdated = value => incoming = value,
        };
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(
                static () => Group(("a", "a"), ("b", "b")),
                properties),
            host.Container);
        host.RunScheduledFlushes();
        RendererParityNode firstA = Elements(host.Container)[0];
        RendererParityNode firstB = Elements(host.Container)[1];

        renderer.Render(
            Transition(
                static () => Group(("b", "b"), ("c", "c")),
                properties),
            host.Container);

        outgoing.ShouldNotBeNull().Select(item => item.Key).ShouldBe(["a", "b"]);
        outgoing.Select(item => item.Element).ShouldBe([firstA, firstB]);
        incoming.ShouldNotBeNull().Select(item => item.Key).ShouldBe(["b", "c"]);
        incoming[0].Element.ShouldBeSameAs(firstB);
        incoming[1].Element.ShouldBeSameAs(Elements(host.Container)[1]);
    }

    [Fact]
    public void Render_KeyedTransitionRemoval_DefersHostRemovalUntilLeaveCompletes()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionNode item = Transition(
            () => Element("li", "item"),
            recorder.Build(),
            key: "item");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(new FragmentNode([item]), host.Container);
        host.RunScheduledFlushes();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();

        renderer.Render(new FragmentNode([]), host.Container);

        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        element.Parent.ShouldBeSameAs(host.Container);

        recorder.CompleteLeave();

        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
        element.Parent.ShouldBeNull();
    }

    [Fact]
    public void Render_RootTeardown_DrainsDetachedKeyedTransitionLeaveOnce()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionNode item = Transition(
            () => Element("li", "item"),
            recorder.Build(),
            key: "item");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(new FragmentNode([item]), host.Container);
        host.RunScheduledFlushes();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();
        renderer.Render(new FragmentNode([]), host.Container);

        renderer.Render(null, host.Container);

        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
        element.Parent.ShouldBeNull();
        host.Container.Children.ShouldBeEmpty();

        recorder.CompleteLeave();

        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_RemovalDuringOutgoingLeave_SettlesBeforeDeferringCurrentChild()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionProperties properties = recorder.Build(mode: "out-in");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            Transition(() => Element("div", "first"), properties),
            host.Container);
        host.RunScheduledFlushes();
        renderer.Render(
            Transition(() => Element("span", "second"), properties),
            host.Container);

        renderer.Render(new CommentNode("removed"), host.Container);

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "leaveCancelled",
            "beforeLeave",
            "leave",
        ]);
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();
        element.Description.ShouldBe("span");

        recorder.CompleteLeave();

        recorder.Calls[^1].ShouldBe("afterLeave");
        element.Parent.ShouldBeNull();
        Elements(host.Container).ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReinsertSameScopedTransition_CompletesOldLeaveBeforeRegistration()
    {
        using var host = new RendererParityHost();
        Reference<bool> show = Reactive.Reference(true);
        TransitionRecorder recorder = new();
        ReinsertTransitionProbe component = new(show, recorder.Build());
        ComponentReference reference = ComponentReference.ForType(
            typeof(ReinsertTransitionProbe));
        ComponentNode root = new(reference);
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => component));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, Application(root, components: components));
        host.RunScheduledFlushes();
        RendererParityNode outgoing = Elements(host.Container).ShouldHaveSingleItem();
        show.Value = false;
        host.RunScheduledFlushes();

        show.Value = true;
        host.RunScheduledFlushes();

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
        ]);
        outgoing.Parent.ShouldBeNull();
        Elements(host.Container).ShouldHaveSingleItem().ShouldNotBeSameAs(outgoing);
    }

    [Fact]
    public void Render_ComponentTeardown_DrainsRawTransitionSubtreeWithoutStartingLeave()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        RawTransitionRootComponent component = new(recorder.Build());
        ComponentReference reference = ComponentReference.ForType(
            typeof(RawTransitionRootComponent));
        ComponentNode root = new(reference);
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => component));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, Application(root, components: components));
        host.RunScheduledFlushes();

        renderer.Render(new CommentNode("removed"), host.Container);

        recorder.Calls.ShouldBeEmpty();
        Elements(host.Container).ShouldBeEmpty();
    }

    [Fact]
    public void Render_PersistedSameElementUpdate_PreservesDirectiveOwnedEnterAndLeave()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        ComponentTransition? created = null;
        ComponentTransition? updated = null;
        Directive directive = new()
        {
            Created = (_, binding, _, _) => created = binding.Transition,
            Updated = (_, binding, _, _) => updated = binding.Transition,
        };
        TransitionProperties properties = recorder.Build(persisted: true);
        TransitionNode initial = Transition(
            static () => DirectedElement("section"),
            properties);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            initial,
            host.Container,
            Application(initial, directive: directive));
        host.RunScheduledFlushes();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();

        created.ShouldNotBeNull().BeforeEnter(element);
        created.Enter(element);
        renderer.Render(
            Transition(static () => DirectedElement("section"), properties),
            host.Container);

        updated.ShouldBeSameAs(created);
        recorder.Calls.ShouldBe(["beforeEnter", "enter"]);
        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(["beforeEnter", "enter", "afterEnter"]);

        int removals = 0;
        created.Leave(element, () => removals++);
        renderer.Render(
            Transition(static () => DirectedElement("section"), properties),
            host.Container);

        updated.ShouldBeSameAs(created);
        removals.ShouldBe(0);
        recorder.Calls.ShouldBe(
        [
            "beforeEnter",
            "enter",
            "afterEnter",
            "beforeLeave",
            "leave",
        ]);

        recorder.CompleteLeave();
        removals.ShouldBe(1);
        recorder.Calls[^1].ShouldBe("afterLeave");
    }

    [Fact]
    public void Render_NestedTransitionAfterClaim_BindsOnlyRootsAndRunsBothAppearPhases()
    {
        using var host = new RendererParityHost();
        TransitionRecorder outer = new();
        TransitionRecorder inner = new();
        Dictionary<string, ComponentTransition?> bindings = new(StringComparer.Ordinal);
        Directive directive = new()
        {
            Created = (element, binding, _, _) =>
                bindings[((RendererParityNode)element).Description] = binding.Transition,
        };
        TransitionNode nested = Transition(
            () => new ElementNode(
                new QualifiedName("section"),
                children:
                [
                    DirectedElement("paragraph"),
                    Transition(
                        static () => DirectedElement("span"),
                        inner.Build(appear: true)),
                ],
                directives:
                [
                    new DirectiveInvocation(typeof(TransitionDirectiveToken)),
                ]),
            outer.Build(appear: true));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(
            nested,
            host.Container,
            Application(nested, directive: directive));

        outer.Calls.ShouldBe(["beforeAppear", "appear"]);
        inner.Calls.ShouldBe(["beforeAppear", "appear"]);
        bindings["section"].ShouldNotBeNull();
        bindings["paragraph"].ShouldBeNull();
        bindings["span"].ShouldNotBeNull().ShouldNotBeSameAs(bindings["section"]);
    }

    [Fact]
    public void Render_GroupSnapshots_TraverseUnkeyedElementComponentAndStructuralWrappers()
    {
        using var host = new RendererParityHost();
        IReadOnlyList<TransitionElementSnapshot>? outgoing = null;
        IReadOnlyList<TransitionElementSnapshot>? incoming = null;
        TransitionProperties properties = new()
        {
            OnBeforeUpdate = value => outgoing = value,
            OnUpdated = value => incoming = value,
        };
        ComponentReference reference = ComponentReference.ForType(
            typeof(SnapshotWrapperComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    parameters: [new ComponentParameter("text")]),
                _ => new SnapshotWrapperComponent()));
        TransitionNode initial = Transition(
            () => SnapshotGroup(reference, "initial"),
            properties);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            initial,
            host.Container,
            Application(initial, components: components));
        host.RunScheduledFlushes();

        renderer.Render(
            Transition(() => SnapshotGroup(reference, "updated"), properties),
            host.Container);

        outgoing.ShouldNotBeNull().Select(item => item.Key).ShouldBe(["a", "b", "c"]);
        incoming.ShouldNotBeNull().Select(item => item.Key).ShouldBe(["a", "b", "c"]);
        for (int index = 0; index < outgoing.Count; index++)
        {
            incoming[index].Element.ShouldBeSameAs(outgoing[index].Element);
        }
    }

    [Fact]
    public void ComponentTransition_RemovalFailure_RoutesAndSettlesNormalAndDisposedLeaves()
    {
        using var host = new RendererParityHost();
        List<(Exception Exception, string Information)> failures = [];
        TransitionRecorder recorder = new();
        ComponentTransition? bound = null;
        Directive directive = new()
        {
            Created = (_, binding, _, _) => bound = binding.Transition,
        };
        TransitionNode transition = Transition(
            static () => DirectedElement("section"),
            recorder.Build(persisted: true));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            transition,
            host.Container,
            Application(
                transition,
                directive: directive,
                errorHandler: (exception, _, information) =>
                    failures.Add((exception, information))));
        host.RunScheduledFlushes();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();
        int removals = 0;

        bound.ShouldNotBeNull().Leave(
            element,
            () =>
            {
                removals++;
                throw new InvalidOperationException("normal removal failed");
            });
        recorder.CompleteLeave();

        removals.ShouldBe(1);
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
        failures.ShouldHaveSingleItem().Information.ShouldBe("transition removal callback");

        renderer.Render(null, host.Container);
        bound.Leave(
            element,
            () =>
            {
                removals++;
                throw new InvalidOperationException("disposed removal failed");
            });

        removals.ShouldBe(2);
        failures.Count.ShouldBe(2);
        failures[1].Information.ShouldBe("transition removal callback");
        recorder.CompleteLeave();
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);
    }

    [Fact]
    public void ComponentTransitionScope_NewChildSharesMountedStateAndCanForceEnterCompletion()
    {
        using var host = new RendererParityHost();
        Reference<bool> showSecond = Reactive.Reference(false);
        TransitionScopeProbe component = new(showSecond);
        ComponentReference reference = ComponentReference.ForType(typeof(TransitionScopeProbe));
        ComponentNode root = new(reference);
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => component));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, Application(root, components: components));
        host.RunScheduledFlushes();

        showSecond.Value = true;
        host.RunScheduledFlushes();

        component.EnterCount.ShouldBe(1);
        component.AfterEnterCount.ShouldBe(0);
        RendererParityNode pending = Elements(host.Container)[1];
        component.Scope.ShouldNotBeNull()
            .FinishPendingEnter(pending)
            .ShouldBeTrue();
        component.AfterEnterCount.ShouldBe(1);
        component.Scope.FinishPendingEnter(pending).ShouldBeFalse();
    }

    [Fact]
    public void ComponentTransitionScope_MatchingIncomingIdentity_CompletesPendingLeave()
    {
        using var host = new RendererParityHost();
        Reference<bool> replace = Reactive.Reference(false);
        TransitionRecorder recorder = new();
        TransitionIdentityProbe component = new(replace, recorder.Build());
        ComponentReference reference = ComponentReference.ForType(
            typeof(TransitionIdentityProbe));
        ComponentNode root = new(reference);
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => component));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(root, host.Container, Application(root, components: components));
        host.RunScheduledFlushes();

        replace.Value = true;
        host.RunScheduledFlushes();

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
        ]);
        Elements(host.Container).ShouldHaveSingleItem().DescendantText.ShouldBe("replacement");
    }

    [Fact]
    public void Render_PersistedTransition_BindsFirstNestedElementDirectiveWithoutRendererPhases()
    {
        using var host = new RendererParityHost();
        TransitionRecorder outer = new();
        TransitionRecorder inner = new();
        ComponentTransition? bound = null;
        Directive directive = new()
        {
            Created = (_, binding, _, _) => bound = binding.Transition,
        };
        ElementNode child = DirectedElement("section");
        TransitionNode nested = Transition(
            () => Transition(
                () => new FragmentNode([new CommentNode("lead"), child]),
                inner.Build(persisted: true)),
            outer.Build(persisted: true));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(
            nested,
            host.Container,
            Application(nested, directive: directive));
        host.RunScheduledFlushes();

        outer.Calls.ShouldBeEmpty();
        inner.Calls.ShouldBeEmpty();
        bound.ShouldNotBeNull().IsPersisted.ShouldBeTrue();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();
        bound.BeforeEnter(element);
        bound.Enter(element);
        inner.Calls.ShouldBe(["beforeEnter", "enter"]);
        outer.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void ComponentTransition_InterruptedEnterAndLeave_CancelPreviousPhaseOnce()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        ComponentTransition? bound = null;
        Directive directive = new()
        {
            Created = (_, binding, _, _) => bound = binding.Transition,
        };
        TransitionNode transition = Transition(
            static () => DirectedElement("section"),
            recorder.Build(persisted: true));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(
            transition,
            host.Container,
            Application(transition, directive: directive));
        host.RunScheduledFlushes();
        RendererParityNode element = Elements(host.Container).ShouldHaveSingleItem();
        int removals = 0;

        bound.ShouldNotBeNull().BeforeEnter(element);
        bound.Enter(element);
        bound.Leave(element, () => removals++);

        recorder.Calls.ShouldBe(
        [
            "beforeEnter",
            "enter",
            "enterCancelled",
            "beforeLeave",
            "leave",
        ]);
        removals.ShouldBe(0);

        bound.BeforeEnter(element);
        bound.Enter(element);
        recorder.CompleteLeave();

        removals.ShouldBe(1);
        recorder.Calls.ShouldBe(
        [
            "beforeEnter",
            "enter",
            "enterCancelled",
            "beforeLeave",
            "leave",
            "leaveCancelled",
            "beforeEnter",
            "enter",
        ]);
    }

    [Fact]
    public void Hydrate_PersistedTransition_BindsDirectiveAndSuppressesInitialAppear()
    {
        Scheduler.Reset();
        Queue<Action> scheduled = [];
        using IDisposable registration = Scheduler.UseFlushDispatcher(scheduled.Enqueue);
        try
        {
            HydrationWalkerFakeHost host = new();
            HydrationWalkerHostNode serverElement = host.CreateServerElement("section");
            host.AppendServerChild(host.Root, serverElement);
            TransitionRecorder recorder = new();
            ComponentTransition? bound = null;
            Directive directive = new()
            {
                Created = (_, binding, _, _) => bound = binding.Transition,
                Mounted = (element, binding, _, _) =>
                {
                    binding.Transition!.BeforeEnter(element);
                    binding.Transition.Enter(element);
                },
            };
            TransitionNode transition = Transition(
                static () => DirectedElement("section"),
                recorder.Build(appear: true, persisted: true));
            Renderer<HydrationWalkerHostNode> renderer =
                RendererFactory.CreateRenderer(host.Options);

            renderer.Hydrate(
                transition,
                host.Root,
                Application(transition, directive: directive));
            while (scheduled.Count > 0)
            {
                scheduled.Dequeue()();
            }

            bound.ShouldNotBeNull().IsPersisted.ShouldBeTrue();
            recorder.Calls.ShouldBeEmpty();
            host.Root.Children.ShouldBe([serverElement]);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void Unmount_PendingAppear_CancelsAndDrainsOperationExactlyOnce()
    {
        using var host = new RendererParityHost();
        TransitionRecorder recorder = new();
        TransitionNode transition = Transition(
            () => Element("div", "content"),
            recorder.Build(appear: true));
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(transition, host.Container);

        renderer.Render(null, host.Container);
        recorder.CompleteEnter();

        recorder.Calls.ShouldBe(["beforeAppear", "appear", "appearCancelled"]);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_TransitionHookFailure_RoutesToApplicationErrorHandlerOnce()
    {
        using var host = new RendererParityHost();
        List<(Exception Exception, string Information)> failures = [];
        TransitionProperties properties = new()
        {
            Appear = true,
            OnBeforeAppear = _ => throw new InvalidOperationException("transition failed"),
        };
        TransitionNode transition = Transition(
            () => Element("div", "content"),
            properties);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(
            transition,
            host.Container,
            Application(
                transition,
                errorHandler: (exception, _, information) =>
                    failures.Add((exception, information))));

        failures.ShouldHaveSingleItem();
        failures[0].Exception.Message.ShouldBe("transition failed");
        failures[0].Information.ShouldBe("transition before-appear hook");
    }

    private static TransitionNode Transition(
        Func<VirtualNode> child,
        TransitionProperties properties,
        object? key = null)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
        {
            [TransitionProperties.ResolvedArgument] = properties,
        };
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["default"] = _ => child(),
        };
        return new TransitionNode(new ComponentInvocation(arguments, slots), key);
    }

    private static ElementNode Element(string name, string text) =>
        new(
            new QualifiedName(name),
            children: [new TextNode(text)]);

    private static ElementNode DirectedElement(string name) =>
        new(
            new QualifiedName(name),
            directives: [new DirectiveInvocation(typeof(TransitionDirectiveToken))]);

    private static FragmentNode Group(params (string Key, string Text)[] entries)
    {
        List<VirtualNode> children = new(entries.Length);
        for (int index = 0; index < entries.Length; index++)
        {
            (string key, string text) = entries[index];
            children.Add(
                new FragmentNode(
                [
                    new CommentNode("leading"),
                    Element("li", text),
                ],
                    key: key));
        }

        return new FragmentNode(children);
    }

    private static ElementNode SnapshotGroup(
        ComponentReference component,
        string text)
    {
        FragmentNode keyedFragment = new(
        [
            new CommentNode("leading"),
            new FragmentNode(
            [
                Element("li", $"a-{text}"),
            ],
                key: "nested-a"),
        ],
            key: "a");
        ComponentNode componentWrapper = new(
            component,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?>
                {
                    ["text"] = text,
                }));
        TransitionNode structuralWrapper = Transition(
            () => new FragmentNode(
            [
                new CommentNode("leading"),
                new ElementNode(
                    new QualifiedName("li"),
                    children: [new TextNode($"c-{text}")],
                    key: "c"),
            ]),
            new TransitionProperties { Persisted = true });
        return new ElementNode(
            new QualifiedName("ul"),
            children:
            [
                new FragmentNode([keyedFragment]),
                componentWrapper,
                structuralWrapper,
            ]);
    }

    private static RendererParityNode[] Elements(RendererParityNode container) =>
        container.Children
            .Where(node => node.Kind == RendererParityNodeKind.Element)
            .ToArray();

    private static ApplicationContext Application(
        VirtualNode root,
        ComponentFactory? components = null,
        IDirective? directive = null,
        Action<Exception, ComponentContext?, string>? errorHandler = null)
    {
        DirectiveRegistry? directives = directive is null
            ? null
            : new DirectiveRegistry(
            [
                new KeyValuePair<Type, IDirective>(
                    typeof(TransitionDirectiveToken),
                    directive),
            ]);
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components ?? new ComponentFactory(),
                Directives = directives,
                ErrorHandler = errorHandler,
            });
    }

    private sealed class TransitionRecorder
    {
        private Action? _enterComplete;
        private Action? _leaveComplete;

        internal List<string> Calls { get; } = [];

        internal void CompleteEnter()
        {
            Action? complete = _enterComplete;
            _enterComplete = null;
            complete?.Invoke();
        }

        internal void CompleteLeave()
        {
            Action? complete = _leaveComplete;
            _leaveComplete = null;
            complete?.Invoke();
        }

        internal TransitionProperties Build(
            string? mode = null,
            bool appear = false,
            bool persisted = false) =>
            new()
            {
                Mode = mode,
                Appear = appear,
                Persisted = persisted,
                OnBeforeEnter = _ => Calls.Add("beforeEnter"),
                OnEnter = (_, complete) =>
                {
                    Calls.Add("enter");
                    _enterComplete = complete;
                },
                OnAfterEnter = _ => Calls.Add("afterEnter"),
                OnEnterCancelled = _ => Calls.Add("enterCancelled"),
                OnBeforeLeave = _ => Calls.Add("beforeLeave"),
                OnLeave = (_, complete) =>
                {
                    Calls.Add("leave");
                    _leaveComplete = complete;
                },
                OnAfterLeave = _ => Calls.Add("afterLeave"),
                OnLeaveCancelled = _ => Calls.Add("leaveCancelled"),
                OnBeforeAppear = _ => Calls.Add("beforeAppear"),
                OnAppear = (_, complete) =>
                {
                    Calls.Add("appear");
                    _enterComplete = complete;
                },
                OnAfterAppear = _ => Calls.Add("afterAppear"),
                OnAppearCancelled = _ => Calls.Add("appearCancelled"),
            };
    }

    private sealed class TransitionScopeProbe : IComponent
    {
        private readonly Reference<bool> _showSecond;

        internal TransitionScopeProbe(Reference<bool> showSecond)
        {
            _showSecond = showSecond;
        }

        internal ComponentTransitionScope? Scope { get; private set; }

        internal int EnterCount { get; private set; }

        internal int AfterEnterCount { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            Scope = new ComponentTransitionScope(context);
            TransitionProperties properties = new()
            {
                OnEnter = (_, _) => EnterCount++,
                OnAfterEnter = _ => AfterEnterCount++,
            };
            return _ =>
            {
                List<VirtualNode> children =
                [
                    Scope.Attach(
                        _ => Element("span", "a"),
                        properties,
                        key: "a"),
                ];
                if (_showSecond.Value)
                {
                    children.Add(
                        Scope.Attach(
                            _ => Element("span", "b"),
                            properties,
                            key: "b"));
                }

                return new FragmentNode(children);
            };
        }
    }

    private sealed class TransitionIdentityProbe : IComponent
    {
        private readonly TransitionProperties _properties;
        private readonly Reference<bool> _replace;

        internal TransitionIdentityProbe(
            Reference<bool> replace,
            TransitionProperties properties)
        {
            _replace = replace;
            _properties = properties;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ComponentTransitionScope scope = new(context);
            return _ =>
            {
                bool replace = _replace.Value;
                List<VirtualNode> children =
                [
                    scope.Attach(
                        _ => replace
                            ? new CommentNode("outgoing")
                            : new ElementNode(
                                new QualifiedName("div"),
                                children: [new TextNode("outgoing")],
                                key: "shared"),
                        _properties,
                        key: "source"),
                ];
                if (replace)
                {
                    children.Add(
                        scope.Attach(
                            _ => new ElementNode(
                                new QualifiedName("div"),
                                children: [new TextNode("replacement")],
                                key: "shared"),
                            _properties,
                            key: "replacement"));
                }

                return new FragmentNode(children);
            };
        }
    }

    private sealed class RawTransitionRootComponent : IComponent
    {
        private readonly TransitionProperties _properties;

        internal RawTransitionRootComponent(TransitionProperties properties)
        {
            _properties = properties;
        }

        public ComponentRenderer Setup(ComponentContext context) =>
            _ => Transition(
                static () => Element("section", "component"),
                _properties);
    }

    private sealed class SnapshotWrapperComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new FragmentNode(
            [
                new CommentNode("leading"),
                new ElementNode(
                    new QualifiedName("li"),
                    children:
                    [
                        new TextNode($"b-{context.Bindings.Parameters["text"]}"),
                    ],
                    key: "b"),
            ]);
    }

    private sealed class ReinsertTransitionProbe : IComponent
    {
        private readonly TransitionProperties _properties;
        private readonly Reference<bool> _show;

        internal ReinsertTransitionProbe(
            Reference<bool> show,
            TransitionProperties properties)
        {
            _show = show;
            _properties = properties;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ComponentTransitionScope scope = new(context);
            TransitionNode item = scope.Attach(
                static _ => Element("li", "item"),
                _properties,
                key: "item");
            FragmentNode populated = new([item]);
            FragmentNode empty = new([]);
            return _ => _show.Value ? populated : empty;
        }
    }

    private sealed class TransitionDirectiveToken
    {
    }
}
