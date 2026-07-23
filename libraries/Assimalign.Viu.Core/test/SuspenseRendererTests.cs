using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins host-generic Suspense behavior to Vue 3.5's pending, fallback, and reveal model.
/// </summary>
public sealed class SuspenseRendererTests : IDisposable
{
    private readonly TestSchedulerPump _pump;
    private readonly SynchronizationContext? _previousContext;

    public SuspenseRendererTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new InlineSynchronizationContext());
    }

    [Fact]
    public void Render_PendingDependency_ShowsFallbackThenRevealsResolvedDefault()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition = Define(load);
        ITemplateComponent root = Suspense.CreateComponent(
            _ => definition.CreateComponent(
                Arguments(("message", "resolved"))),
            _ => ComponentTree.Text("loading"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);

        renderer.Render(root, host.Root, CreateApplication(root, definition));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("loading");

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("resolved");
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Render_NamedBuiltIn_UsesSuspenseBoundarySemantics()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition = Define(load);
        ComponentSlots slots = new(SlotFlags.Dynamic)
        {
            ["default"] = _ => definition.CreateComponent(),
            ["fallback"] = _ => ComponentTree.Text("named-fallback"),
        };
        ITemplateComponent root = ComponentTree.Template(
            "Suspense",
            slots: slots);
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);

        renderer.Render(root, host.Root, CreateApplication(root, definition));
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("named-fallback");

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("resolved");
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Render_MultipleDependencies_RevealsOnlyAfterEveryLoadSettles()
    {
        TaskCompletionSource<AsynchronousComponentTarget> firstLoad = new();
        TaskCompletionSource<AsynchronousComponentTarget> secondLoad = new();
        AsynchronousComponentDefinition first = Define<FirstIdentity>(firstLoad);
        AsynchronousComponentDefinition second = Define<SecondIdentity>(secondLoad);
        ITemplateComponent root = Suspense.CreateComponent(
            _ => ComponentTree.Fragment(
            [
                first.CreateComponent(Arguments(("message", "first"))),
                second.CreateComponent(Arguments(("message", "second"))),
            ]),
            _ => ComponentTree.Text("waiting"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, first, second);
        ApplicationContext application = CreateApplication(
            root,
            first,
            second);

        renderer.Render(root, host.Root, application);
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("waiting");

        firstLoad.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("waiting");

        secondLoad.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("firstsecond");
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Render_ParentInvalidationWhilePending_UpdatesFallbackAndHiddenContent()
    {
        Reference<string> message = Reactive.Reference("first");
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition = Define(load);
        ITemplateComponent root = Suspense.CreateComponent(
            _ => definition.CreateComponent(
                Arguments(("message", message.Value))),
            _ => ComponentTree.Text($"loading-{message.Value}"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);

        renderer.Render(root, host.Root, CreateApplication(root, definition));
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("loading-first");

        message.Value = "second";
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("loading-second");

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("second");
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Render_NestedBoundary_OwnsItsDependencyWithoutBlockingOuterBoundary()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition = Define(load);
        ITemplateComponent inner = Suspense.CreateComponent(
            _ => definition.CreateComponent(
                Arguments(("message", "inner-resolved"))),
            _ => ComponentTree.Text("inner-fallback"));
        ITemplateComponent root = Suspense.CreateComponent(
            _ => inner,
            _ => ComponentTree.Text("outer-fallback"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);

        renderer.Render(root, host.Root, CreateApplication(root, definition));
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("inner-fallback");

        load.SetResult(AsynchronousComponentTarget.From<ResolvedTemplate>());
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("inner-resolved");
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Unmount_PendingBoundary_CancelsLastLoadAndIgnoresSettlement()
    {
        int cancellations = 0;
        bool loaderStarted = false;
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<FirstIdentity>(
                async cancellationToken =>
                {
                    loaderStarted = true;
                    _ = cancellationToken.Register(() => cancellations++);
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return AsynchronousComponentTarget.From<ResolvedTemplate>();
                });
        ITemplateComponent root = Suspense.CreateComponent(
            _ => definition.CreateComponent(),
            _ => ComponentTree.Text("waiting"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);

        renderer.Render(root, host.Root, CreateApplication(root, definition));
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("waiting");
        loaderStarted.ShouldBeTrue();
        MountedTemplateNode<FakeHostNode> boundaryMount =
            renderer.GetMountedTemplates(host.Root)[0];
        MountedTemplateNode<FakeHostNode> asynchronousMount =
            boundaryMount.SuspenseState!.ContentBranch
                .ShouldBeOfType<MountedTemplateNode<FakeHostNode>>();
        FakeHostNode storageContainer =
            boundaryMount.SuspenseState.StorageContainer;

        renderer.Render(null, host.Root);
        _pump.RunUntilIdle();

        asynchronousMount.Instance.Context.IsUnmounted.ShouldBeTrue();
        cancellations.ShouldBe(1);
        host.Operations.ShouldContain(
            $"remove:{storageContainer.Identifier}");
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_RejectedDependency_RoutesErrorAndSettlesBoundary()
    {
        TaskCompletionSource<AsynchronousComponentTarget> load = new();
        AsynchronousComponentDefinition definition = Define(load);
        ITemplateComponent root = Suspense.CreateComponent(
            _ => definition.CreateComponent(),
            _ => ComponentTree.Text("waiting"));
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(root, definition);
        ApplicationContext application = CreateApplication(root, definition);
        Exception? handled = null;
        application.ErrorHandler = (exception, _, _) => handled = exception;

        renderer.Render(root, host.Root, application);
        _pump.RunUntilIdle();
        host.Text(host.Root).ShouldBe("waiting");

        load.SetException(new InvalidOperationException("load failed"));
        _pump.RunUntilIdle();

        handled.ShouldBeOfType<InvalidOperationException>();
        handled!.Message.ShouldBe("load failed");
        host.Text(host.Root).ShouldBe(string.Empty);
        renderer.Render(null, host.Root);
    }

    [Fact]
    public void Render_SuspenseRequest_PreservesComponentNodeLifecycleHooks()
    {
        List<string> order = [];
        ITemplateComponent initial = SuspenseLifecycleRequest(
            order,
            "initial");
        (Renderer<FakeHostNode> renderer, FakeHost host) =
            CreateRenderer(initial);

        renderer.Render(
            initial,
            host.Root,
            CreateApplication(initial));
        _pump.RunUntilIdle();

        order.ShouldBe(["before-mount", "mounted"]);
        host.Text(host.Root).ShouldBe("initial");

        order.Clear();
        renderer.Render(
            SuspenseLifecycleRequest(order, "updated"),
            host.Root);
        _pump.RunUntilIdle();

        order.ShouldBe(["before-update", "updated"]);
        host.Text(host.Root).ShouldBe("updated");

        order.Clear();
        renderer.Render(null, host.Root);
        _pump.RunUntilIdle();

        order.ShouldBe(["before-unmount", "unmounted"]);
    }

    public void Dispose()
    {
        SynchronizationContext.SetSynchronizationContext(_previousContext);
        _pump.Dispose();
        Scheduler.Reset();
    }

    private static AsynchronousComponentDefinition Define(
        TaskCompletionSource<AsynchronousComponentTarget> load)
    {
        return Define<FirstIdentity>(load);
    }

    private static AsynchronousComponentDefinition Define<TIdentity>(
        TaskCompletionSource<AsynchronousComponentTarget> load)
        where TIdentity : class
    {
        return AsynchronousComponents.DefineAsynchronousComponent<TIdentity>(
            _ => load.Task);
    }

    private static (
        Renderer<FakeHostNode> Renderer,
        FakeHost Host) CreateRenderer(
        IComponent root,
        params AsynchronousComponentDefinition[] definitions)
    {
        _ = root;
        _ = definitions;
        FakeHost host = new();
        return (RendererFactory.CreateRenderer(host.Options), host);
    }

    private static ApplicationContext CreateApplication(
        IComponent root,
        params AsynchronousComponentDefinition[] definitions)
    {
        List<ComponentRegistration> registrations =
        [
            Suspense.Registration,
            new ComponentRegistration(
                typeof(ResolvedTemplate),
                static () => new ResolvedTemplate()),
        ];
        for (int index = 0; index < definitions.Length; index++)
        {
            registrations.Add(definitions[index].Registration);
        }

        return new ApplicationContext(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceProvider());
    }

    private static ComponentArguments Arguments(
        params (string Name, object? Value)[] values)
    {
        List<KeyValuePair<string, object?>> arguments = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            arguments.Add(
                new KeyValuePair<string, object?>(
                    values[index].Name,
                    values[index].Value));
        }

        return new ComponentArguments(arguments);
    }

    private static ITemplateComponent SuspenseLifecycleRequest(
        List<string> order,
        string content)
    {
        ComponentSlots slots = new(SlotFlags.Dynamic)
        {
            ["default"] = _ => ComponentTree.Text(content),
        };
        return ComponentTree.Template<Suspense>(
            new ComponentArguments(
            [
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeMount",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("before-mount"))),
                new KeyValuePair<string, object?>(
                    "onVnodeMounted",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("mounted"))),
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeUpdate",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("before-update"))),
                new KeyValuePair<string, object?>(
                    "onVnodeUpdated",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("updated"))),
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeUnmount",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("before-unmount"))),
                new KeyValuePair<string, object?>(
                    "onVnodeUnmounted",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("unmounted"))),
            ]),
            slots);
    }

    private sealed class FirstIdentity
    {
    }

    private sealed class SecondIdentity
    {
    }

    private sealed class ResolvedTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("message"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Text(
                context.Arguments.Get<string>("message") ?? "resolved");
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(
            SendOrPostCallback callback,
            object? state)
        {
            callback(state);
        }

        public override void Send(
            SendOrPostCallback callback,
            object? state)
        {
            callback(state);
        }
    }
}
