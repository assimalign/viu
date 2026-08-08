using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins nested and multi-dependency Suspense ownership around asynchronous components [BLT-11]
/// and the explicitly retained boundary limits [BLT-13].
/// </summary>
public sealed class RendererSuspenseParityTests
{
    [Fact]
    public async Task Suspense_MultipleDependencies_RevealsOnlyAfterEveryLoadSettles()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> firstLoad = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<AsynchronousComponentTarget> secondLoad = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition first = Define<FirstWrapper>(firstLoad);
        AsynchronousComponentDefinition second = Define<SecondWrapper>(secondLoad);
        SuspenseNode root = Suspense(
            new FragmentNode(
            [
                Request(first, "first"),
                Request(second, "second"),
            ]),
            new TextNode("waiting"));
        ComponentFactory components = CreateFactory(first, second);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, components));

        VisibleText(host.Container).ShouldBe("waiting");

        firstLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        VisibleText(host.Container).ShouldBe("waiting");

        secondLoad.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        VisibleText(host.Container).ShouldBe("firstsecond");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task Suspense_NestedBoundary_OwnsDependencyWithoutBlockingOuterBoundary()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        SuspenseNode inner = Suspense(
            Request(definition, "inner-resolved"),
            new TextNode("inner-fallback"));
        SuspenseNode root = Suspense(inner, new TextNode("outer-fallback"));
        ComponentFactory components = CreateFactory(definition);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, CreateApplication(root, components));

        VisibleText(host.Container).ShouldBe("inner-fallback");

        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        VisibleText(host.Container).ShouldBe("inner-resolved");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task Suspense_UpdateWhilePending_RefreshesFallbackAndHiddenContentBeforeReveal()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        SuspenseNode initial = Suspense(
            Request(definition, "first"),
            new TextNode("loading-first"));
        ComponentFactory components = CreateFactory(definition);
        ApplicationContext application = CreateApplication(initial, components);
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(initial, host.Container, application);

        VisibleText(host.Container).ShouldBe("loading-first");

        SuspenseNode updated = Suspense(
            Request(definition, "second"),
            new TextNode("loading-second"));
        renderer.Render(updated, host.Container);

        VisibleText(host.Container).ShouldBe("loading-second");

        load.SetResult(AsynchronousComponentTarget.From<SuspenseTargetComponent>());
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        VisibleText(host.Container).ShouldBe("second");
        renderer.Render(null, host.Container);
    }

    [Fact]
    public async Task Suspense_RejectedDependency_RoutesOnceAndRevealsSettledEmptyBranch()
    {
        using var host = new RendererParityHost();
        TaskCompletionSource<AsynchronousComponentTarget> load = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition = Define<FirstWrapper>(load);
        SuspenseNode root = Suspense(
            definition.CreateComponent(),
            new TextNode("waiting"));
        ComponentFactory components = CreateFactory(definition);
        List<Exception> handled = [];
        ApplicationContext application = CreateApplication(
            root,
            components,
            new ApplicationOptions
            {
                ErrorHandler = (error, _, _) => handled.Add(error),
            });
        Renderer<RendererParityNode> renderer = host.CreateRenderer();

        renderer.Render(root, host.Container, application);
        VisibleText(host.Container).ShouldBe("waiting");

        load.SetException(new InvalidOperationException("load failed"));
        await WaitForPendingSchedulerFlushAsync();
        host.RunScheduledFlushes();

        handled.ShouldHaveSingleItem()
            .ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("load failed");
        VisibleText(host.Container).ShouldBe(string.Empty);
        renderer.Render(null, host.Container);
    }

    private static AsynchronousComponentDefinition Define<TWrapper>(
        TaskCompletionSource<AsynchronousComponentTarget> load)
        where TWrapper : class, IComponent =>
        AsynchronousComponents.DefineAsynchronousComponent<TWrapper>(_ => load.Task);

    private static ComponentNode Request(
        AsynchronousComponentDefinition definition,
        string message) =>
        definition.CreateComponent(
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = message }));

    private static SuspenseNode Suspense(
        VirtualNode content,
        VirtualNode fallback) =>
        new(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => content,
                    ["fallback"] = _ => fallback,
                }));

    private static ComponentFactory CreateFactory(
        params AsynchronousComponentDefinition[] definitions)
    {
        var components = new ComponentFactory();
        for (int index = 0; index < definitions.Length; index++)
        {
            components.Register(definitions[index].Registration);
        }

        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(SuspenseTargetComponent)),
                new ComponentContract(
                    parameters: [new ComponentParameter("message")]),
                _ => new SuspenseTargetComponent()));
        return components;
    }

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        IComponentFactory components,
        ApplicationOptions? configured = null)
    {
        ApplicationOptions options = configured ?? new ApplicationOptions();
        options.RootComponent = root;
        options.Components = components;
        return new ApplicationContext(options);
    }

    private static async Task WaitForPendingSchedulerFlushAsync()
    {
        for (int attempt = 0; attempt < 5000; attempt++)
        {
            if (Scheduler.IsFlushPending)
            {
                return;
            }

            await Task.Delay(1);
        }

        throw new InvalidOperationException(
            "The Suspense dependency did not schedule renderer work.");
    }

    private static string VisibleText(RendererParityNode node)
    {
        if (node.Kind == RendererParityNodeKind.Text)
        {
            return node.Text ?? string.Empty;
        }

        string text = string.Empty;
        for (int index = 0; index < node.Children.Count; index++)
        {
            text = string.Concat(text, VisibleText(node.Children[index]));
        }

        return text;
    }

    private sealed class FirstWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class SecondWrapper : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class SuspenseTargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode(
                context.Bindings.Parameters.TryGetValue(
                    "message",
                    out object? message)
                        ? (string?)message ?? "resolved"
                        : "resolved");
    }
}
