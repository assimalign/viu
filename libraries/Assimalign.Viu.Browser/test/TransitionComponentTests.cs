using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Browser.Tests;

// Pins Browser's TransitionNode.Invocation contract and CSS ownership [BLT-7..9].
public sealed class TransitionComponentTests
{
    [Fact]
    public void Transition_Render_CarriesLazySlotAndResolvedProperties()
    {
        int enterCount = 0;
        int completionCount = 0;
        ElementNode child = new(new QualifiedName("article"), key: "content");
        using var context = new StubComponentContext(
            new ComponentBindings(
                new Dictionary<string, object?>
                {
                    ["css"] = false,
                    ["mode"] = "out-in",
                    ["appear"] = true,
                    ["persisted"] = true,
                    ["onEnter"] = (Action<object>)(_ => enterCount++),
                },
                new Dictionary<string, ComponentSlot>
                {
                    ["default"] = _ => child,
                }));
        IComponent component = Transition.Registration.Activator(null);
        ComponentRenderer render = component.Setup(context);

        TransitionNode result = render(new ComponentRenderFrame())
            .ShouldBeOfType<TransitionNode>();
        TransitionProperties properties = result.Invocation.Arguments[
            TransitionProperties.ResolvedArgument].ShouldBeOfType<TransitionProperties>();
        VirtualNode? renderedChild = result.Invocation.Slots["default"](
            new Dictionary<string, object?>());
        properties.OnEnter!(17, () => completionCount++);

        renderedChild.ShouldBeSameAs(child);
        properties.Mode.ShouldBe("out-in");
        properties.Appear.ShouldBeTrue();
        properties.Persisted.ShouldBeTrue();
        enterCount.ShouldBe(1);
        completionCount.ShouldBe(1);
    }

    [Fact]
    public void Transition_BeforeEnter_QueuesResolvedCssClassesThroughTheBrowserHost()
    {
        using var context = new StubComponentContext(
            new ComponentBindings(
                new Dictionary<string, object?>
                {
                    ["name"] = "fade",
                    ["enterFromClass"] = "custom-from",
                },
                new Dictionary<string, ComponentSlot>
                {
                    ["default"] = _ => new ElementNode(new QualifiedName("div")),
                }));
        TransitionNode result = Transition.Registration.Activator(null)
            .Setup(context)(new ComponentRenderFrame())
            .ShouldBeOfType<TransitionNode>();
        TransitionProperties properties = result.Invocation.Arguments[
            TransitionProperties.ResolvedArgument].ShouldBeOfType<TransitionProperties>();
        List<byte[]> frames = [];
        var host = new BrowserRendererHost(
            (frame, length) =>
            {
                frames.Add(frame.AsSpan(0, length).ToArray());
                return [];
            });
        host.ObserveForeignHandle(77);
        using IDisposable activation = host.Activate();

        properties.OnBeforeEnter!(77);
        host.Options.Commit!();

        string frameText = Encoding.UTF8.GetString(
            frames.SelectMany(static frame => frame).ToArray());
        frameText.ShouldContain("custom-from");
        frameText.ShouldContain("fade-enter-active");
        host.InteropCallCount.ShouldBe(1);
    }

    [Fact]
    public void TransitionGroup_Render_UsesOuterSnapshotObserverAndKeyedChildTransitions()
    {
        ElementNode keyed = new(new QualifiedName("li"), key: "first");
        ElementNode unkeyed = new(new QualifiedName("li"));
        using var context = new StubComponentContext(
            new ComponentBindings(
                new Dictionary<string, object?>
                {
                    ["name"] = "list",
                },
                new Dictionary<string, ComponentSlot>
                {
                    ["default"] = _ => new FragmentNode([keyed, unkeyed]),
                }));
        IComponent component = TransitionGroup.Registration.Activator(null);

        TransitionNode outer = component.Setup(context)(new ComponentRenderFrame())
            .ShouldBeOfType<TransitionNode>();
        TransitionProperties observer = outer.Invocation.Arguments[
            TransitionProperties.ResolvedArgument].ShouldBeOfType<TransitionProperties>();
        FragmentNode group = outer.Invocation.Slots["default"](
            new Dictionary<string, object?>()).ShouldBeOfType<FragmentNode>();
        TransitionNode transitioned = group.Children[0].ShouldBeOfType<TransitionNode>();
        TransitionProperties childProperties = transitioned.Invocation.Arguments[
            TransitionProperties.ResolvedArgument].ShouldBeOfType<TransitionProperties>();

        observer.OnBeforeUpdate.ShouldNotBeNull();
        observer.OnUpdated.ShouldNotBeNull();
        transitioned.Key.ShouldBe("first");
        transitioned.Invocation.Slots["default"](
            new Dictionary<string, object?>()).ShouldBeSameAs(keyed);
        childProperties.OnBeforeEnter.ShouldNotBeNull();
        group.Children[1].ShouldBeSameAs(unkeyed);
        context.Warnings.ShouldBe(["<TransitionGroup> children must be keyed."]);
    }

    [Fact]
    public void TransitionGroup_TaggedRoot_FallthroughUpdatesClassStyleAndAttribute()
    {
        ComponentNode Group(string cssClass, string style, string marker) => new(
            TransitionGroup.Registration.Reference,
            new ComponentInvocation(
                new Dictionary<string, object?>
                {
                    ["tag"] = "ul",
                    ["class"] = cssClass,
                    ["style"] = style,
                    ["data-marker"] = marker,
                },
                new Dictionary<string, ComponentSlot>
                {
                    ["default"] = _ => new FragmentNode([]),
                }));

        Scheduler.Reset();
        Action? scheduledFlush = null;
        using IDisposable dispatcher = Scheduler.UseFlushDispatcher(
            flush => scheduledFlush = flush);
        try
        {
            List<byte[]> frames = [];
            var host = new BrowserRendererHost(
                (frame, length) =>
                {
                    frames.Add(frame.AsSpan(0, length).ToArray());
                    return [];
                });
            host.ObserveForeignHandle(125);
            using IDisposable activation = host.Activate();
            var components = new ComponentFactory();
            components.Register(TransitionGroup.Registration);
            ComponentNode initial = Group("initial", "color:red", "first");
            var application = new ApplicationContext(
                new ApplicationOptions
                {
                    RootComponent = initial,
                    Components = components,
                });
            Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
            renderer.Render(initial, 125, application);
            Drain(ref scheduledFlush);
            frames.Clear();

            renderer.Render(
                Group("updated", "color:blue", "second"),
                125,
                application);
            Drain(ref scheduledFlush);

            string updateFrameText = Encoding.UTF8.GetString(
                frames.SelectMany(static frame => frame).ToArray());
            updateFrameText.ShouldContain("updated");
            updateFrameText.ShouldContain("color:blue");
            updateFrameText.ShouldContain("data-marker");
            updateFrameText.ShouldContain("second");

            renderer.Render(null, 125, application);
            Drain(ref scheduledFlush);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    [Fact]
    public void BrowserHost_TransitionParentRead_FlushesAndUsesTheSuppliedHostParent()
    {
        int parentReadCount = 0;
        var host = new BrowserRendererHost(
            (_, _) => [],
            parentNode: node =>
            {
                parentReadCount++;
                node.ShouldBe(40);
                return 41;
            });
        host.ObserveForeignHandle(40);

        int parent = host.Options.ParentNode(40);

        parent.ShouldBe(41);
        parentReadCount.ShouldBe(1);
    }

    private static void Drain(ref Action? scheduledFlush)
    {
        while (scheduledFlush is { } flush)
        {
            scheduledFlush = null;
            flush();
        }
    }

    private sealed class StubComponentContext : ComponentContext, IDisposable
    {
        private readonly EffectScope _scope = Reactive.EffectScope();

        internal StubComponentContext(ComponentBindings bindings)
        {
            Bindings = bindings;
        }

        public List<string> Warnings { get; } = [];

        public override ComponentBindings Bindings { get; }

        public override IServiceProvider? Services => null;

        public override ComponentLifecycle Lifecycle { get; } = new();

        public override IReactiveEffectScope Scope => _scope;

        public override IReactiveWatchScheduler? WatchScheduler => null;

        public override ComponentContext? Parent => null;

        public override void Emit(string name, params object?[] arguments)
        {
        }

        public override void Expose(object? value)
        {
        }

        public override void Warn(string message) => Warnings.Add(message);

        public void Dispose()
        {
            Lifecycle.Dispose();
            _scope.Dispose();
        }

        protected override void OnWatchError(Exception exception)
        {
            throw exception;
        }
    }
}
