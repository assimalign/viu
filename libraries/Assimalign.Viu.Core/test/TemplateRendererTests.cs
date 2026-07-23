using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_TemplateReactiveInvalidation_PatchesAndUnmountsOneOwnedSubtree()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> lifecycle = [];
        Reference<string> text = Reactive.Reference("before");
        ReactiveTemplate template = new(text, lifecycle);
        ITemplateComponent request = ComponentTree.Template<ReactiveTemplate>();
        IApplicationContext application = CreateApplication(
            request,
            new ComponentRegistration(typeof(ReactiveTemplate), () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponentContext? context = renderer.Render(
            request,
            host.Root,
            application);

        context.ShouldNotBeNull();
        host.Text(host.Root).ShouldBe("before");
        lifecycle.ShouldBe(["setup", "before-mount", "mounted"]);
        FakeHostNode element = host.Root.Children.Single();
        pump.RunUntilIdle();

        text.Value = "after";
        pump.PendingFlushCount.ShouldBe(1);
        pump.RunUntilIdle();

        host.Root.Children.Single().ShouldBeSameAs(element);
        host.Text(host.Root).ShouldBe("after");
        lifecycle.ShouldBe(
        [
            "setup",
            "before-mount",
            "mounted",
            "before-update",
            "updated",
        ]);

        renderer.Render(null, host.Root);
        lifecycle.ShouldBe(
        [
            "setup",
            "before-mount",
            "mounted",
            "before-update",
            "updated",
            "before-unmount",
            "scope-disposed",
            "unmounted",
            "template-disposed",
        ]);
        host.Root.Children.ShouldBeEmpty();

        text.Value = "ignored";
        pump.RunUntilIdle();
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ParentArgumentUpdate_ReusesTemplateAndHostIdentity()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ArgumentsTemplate template = new();
        ITemplateComponent initial = RequestWithTitle("before");
        IApplicationContext application = CreateApplication(
            initial,
            new ComponentRegistration(typeof(ArgumentsTemplate), () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        pump.RunUntilIdle();
        FakeHostNode element = host.Root.Children.Single();

        renderer.Render(RequestWithTitle("after"), host.Root);
        pump.RunUntilIdle();

        host.Root.Children.Single().ShouldBeSameAs(element);
        host.Text(host.Root).ShouldBe("after");
        template.SetupCount.ShouldBe(1);
        template.RenderCount.ShouldBe(2);
        template.BeforeUpdateCount.ShouldBe(1);
        template.UpdatedCount.ShouldBe(1);
    }

    [Fact]
    public void Render_DeclaredListenerOnlyUpdate_UsesLatestListenerWithoutRendering()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> received = [];
        EventTemplate template = new();
        ITemplateComponent initial = ComponentTree.Template<EventTemplate>(
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["save"] = ComponentEventListener.ForArguments(
                    _ => received.Add("before")),
            });
        IApplicationContext application = CreateApplication(
            initial,
            new ComponentRegistration(typeof(EventTemplate), () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponentContext? context = renderer.Render(
            initial,
            host.Root,
            application);
        pump.RunUntilIdle();
        context.ShouldNotBeNull();
        context.Emit("save");

        renderer.Render(
            ComponentTree.Template<EventTemplate>(
                listeners: new Dictionary<string, ComponentEventListener>
                {
                    ["save"] = ComponentEventListener.ForArguments(
                        _ => received.Add("after")),
                }),
            host.Root);
        pump.RunUntilIdle();
        context.Emit("save");

        received.ShouldBe(["before", "after"]);
        template.RenderCount.ShouldBe(1);
    }

    [Fact]
    public void Render_TemplateNodeHooks_InterleaveWithInstanceLifecycle()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> order = [];
        LifecycleOrderTemplate template = new(order);
        ITemplateComponent initial = LifecycleOrderRequest(order, "before");
        IApplicationContext application = CreateApplication(
            initial,
            new ComponentRegistration(
                typeof(LifecycleOrderTemplate),
                () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        pump.RunUntilIdle();

        order.ShouldBe(
        [
            "setup",
            "component-before-mount",
            "node-before-mount",
            "component-mounted",
            "node-mounted",
        ]);

        order.Clear();
        renderer.Render(
            LifecycleOrderRequest(order, "after"),
            host.Root);
        pump.RunUntilIdle();

        order.ShouldBe(
        [
            "component-before-update",
            "node-before-update",
            "component-updated",
            "node-updated",
        ]);

        order.Clear();
        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        order.ShouldBe(
        [
            "node-before-unmount",
            "component-before-unmount",
            "component-unmounted",
            "node-unmounted",
        ]);
    }

    [Fact]
    public void Render_ChildNestedBelowElement_PropagatesParentErrorCaptureContext()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        CapturingParentTemplate parent = new();
        ThrowingChildTemplate child = new();
        ITemplateComponent root = ComponentTree.Template<CapturingParentTemplate>();
        int applicationErrors = 0;
        IApplicationContext application = CreateApplication(
            root,
            new ComponentRegistration(
                typeof(CapturingParentTemplate),
                () => parent),
            new ComponentRegistration(
                typeof(ThrowingChildTemplate),
                () => child));
        application.ErrorHandler = (_, _, _) => applicationErrors++;
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();

        parent.CapturedMessages.ShouldBe(["child render failed"]);
        applicationErrors.ShouldBe(0);
        host.Root.Children.Single().Kind.ShouldBe(FakeHostNodeKind.Element);
        host.Root.Children.Single().Children.Single().Kind
            .ShouldBe(FakeHostNodeKind.Comment);
    }

    [Fact]
    public void Render_KeyedTemplateChildren_MoveTheirExistingHostSubtrees()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        IFragmentComponent initial = ComponentTree.Fragment(
        [
            Item("a", "A"),
            Item("b", "B"),
        ]);
        IApplicationContext application = CreateApplication(
            initial,
            new ComponentRegistration(
                typeof(ItemTemplate),
                static () => new ItemTemplate()));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(initial, host.Root, application);
        pump.RunUntilIdle();
        FakeHostNode[] original = Elements(host.Root);

        host.Operations.Clear();
        renderer.Render(
            ComponentTree.Fragment(
            [
                Item("b", "B"),
                Item("a", "A"),
            ]),
            host.Root);
        pump.RunUntilIdle();

        FakeHostNode[] reordered = Elements(host.Root);
        reordered.Select(host.Text).ShouldBe(["B", "A"]);
        reordered.ShouldBe([original[1], original[0]]);
        host.Operations.Count(
                operation => operation.StartsWith("insert:", StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Fact]
    public void Render_TemplateRoot_AppliesFallthroughAttributesAndStyleScope()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        ScopedTemplate template = new();
        ITemplateComponent root = ScopedRequest("first");
        IApplicationContext application = CreateApplication(
            root,
            new ComponentRegistration(typeof(ScopedTemplate), () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();

        FakeHostNode element = host.Root.Children.Single();
        element.Attributes["class"].ShouldBe("owned");
        element.Attributes["data-id"].ShouldBe("first");
        element.Attributes.ShouldContainKey("data-viu-scoped");

        renderer.Render(ScopedRequest("second"), host.Root);
        pump.RunUntilIdle();

        host.Root.Children.Single().ShouldBeSameAs(element);
        element.Attributes["data-id"].ShouldBe("second");
    }

    [Fact]
    public void Render_TemplateWithoutApplicationContext_ThrowsBeforeActivation()
    {
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => renderer.Render(
                ComponentTree.Template<ArgumentsTemplate>(),
                host.Root));

        exception.Message.ShouldContain("application context");
        host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_BufferedHostCommit_PrecedesMountedAndUpdatedCallbacks()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        int commitCount = 0;
        List<int> observedCommits = [];
        Reference<string> text = Reactive.Reference("before");
        CommitTemplate template = new(
            text,
            () => observedCommits.Add(commitCount));
        ITemplateComponent root = ComponentTree.Template<CommitTemplate>();
        IApplicationContext application = CreateApplication(
            root,
            new ComponentRegistration(typeof(CommitTemplate), () => template));
        FakeHost host = new(() => commitCount++);
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();

        commitCount.ShouldBe(1);
        observedCommits.ShouldBe([1]);

        text.Value = "after";
        pump.RunUntilIdle();

        commitCount.ShouldBe(2);
        observedCommits.ShouldBe([1, 2]);
    }

    [Fact]
    public void ComponentHost_FragmentTemplate_ExposesOnlyOutermostHostElements()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HostAccessTemplate template = new();
        ITemplateComponent root = ComponentTree.Template<HostAccessTemplate>();
        IApplicationContext application = CreateApplication(
            root,
            new ComponentRegistration(typeof(HostAccessTemplate), () => template));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        IComponentContext context = renderer.Render(
            root,
            host.Root,
            application)!;
        pump.RunUntilIdle();

        template.RootElements.ShouldBe(
        [
            host.Root.Children[1],
            host.Root.Children[2],
        ]);

        renderer.Render(null, host.Root);

        ComponentHost.GetRootElements<FakeHostNode>(context).ShouldBeEmpty();
    }

    [Fact]
    public void Render_FunctionThrowingMidBlock_DoesNotCorruptLaterBlockCollection()
    {
        RenderHelpers.ResetBlockTrackingForTests();

        try
        {
            ITemplateComponent root = ComponentTree.Template<ThrowingBlockTemplate>();
            IApplicationContext application = CreateApplication(
                root,
                new ComponentRegistration(
                    typeof(ThrowingBlockTemplate),
                    static () => new ThrowingBlockTemplate()));
            int handled = 0;
            application.ErrorHandler = (_, _, _) => handled++;
            FakeHost host = new();
            Renderer<FakeHostNode> renderer =
                RendererFactory.CreateRenderer(host.Options);

            renderer.Render(root, host.Root, application);

            handled.ShouldBe(1);

            BlockToken block = RenderHelpers._openBlock();
            ITextComponent dynamic = RenderHelpers._createTextVNode(
                "dynamic",
                (int)PatchFlags.Text);
            IElementComponent later = RenderHelpers._createElementBlock(
                block,
                "div",
                null,
                new object?[]
                {
                    RenderHelpers._createTextVNode("static"),
                    dynamic,
                }).ShouldBeAssignableTo<IElementComponent>();

            later.Optimization.DynamicChildren.ShouldBe([dynamic]);
        }
        finally
        {
            RenderHelpers.ResetBlockTrackingForTests();
        }
    }

    private static IApplicationContext CreateApplication(
        IComponent root,
        params ComponentRegistration[] registrations)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceProvider());
    }

    private static ITemplateComponent RequestWithTitle(string title)
    {
        return ComponentTree.Template<ArgumentsTemplate>(
            new ComponentArguments(
            [
                new KeyValuePair<string, object?>("title", title),
            ]));
    }

    private static ITemplateComponent LifecycleOrderRequest(
        List<string> order,
        string title)
    {
        return ComponentTree.Template<LifecycleOrderTemplate>(
            new ComponentArguments(
            [
                new KeyValuePair<string, object?>("title", title),
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeMount",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-before-mount"))),
                new KeyValuePair<string, object?>(
                    "onVnodeMounted",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-mounted"))),
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeUpdate",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-before-update"))),
                new KeyValuePair<string, object?>(
                    "onVnodeUpdated",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-updated"))),
                new KeyValuePair<string, object?>(
                    "onVnodeBeforeUnmount",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-before-unmount"))),
                new KeyValuePair<string, object?>(
                    "onVnodeUnmounted",
                    (ComponentNodeLifecycleHook)(
                        (_, _) => order.Add("node-unmounted"))),
            ]));
    }

    private static ITemplateComponent Item(string key, string text)
    {
        return ComponentTree.Template<ItemTemplate>(
            new ComponentArguments(
            [
                new KeyValuePair<string, object?>("text", text),
            ]),
            key: key);
    }

    private static ITemplateComponent ScopedRequest(string identifier)
    {
        return ComponentTree.Template<ScopedTemplate>(
            new ComponentArguments(
            [
                new KeyValuePair<string, object?>("data-id", identifier),
            ]));
    }

    private static FakeHostNode[] Elements(FakeHostNode root)
    {
        return root.Children
            .Where(node => node.Kind == FakeHostNodeKind.Element)
            .ToArray();
    }

    private sealed class ReactiveTemplate : IComponentTemplate, IDisposable
    {
        private readonly List<string> _lifecycle;
        private readonly Reference<string> _text;

        internal ReactiveTemplate(
            Reference<string> text,
            List<string> lifecycle)
        {
            _text = text;
            _lifecycle = lifecycle;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            _lifecycle.Add("setup");
            Reactive.OnScopeDispose(() => _lifecycle.Add("scope-disposed"));
            context.Lifecycle.OnBeforeMount(() => _lifecycle.Add("before-mount"));
            context.Lifecycle.OnMounted(() => _lifecycle.Add("mounted"));
            context.Lifecycle.OnBeforeUpdate(() => _lifecycle.Add("before-update"));
            context.Lifecycle.OnUpdated(() => _lifecycle.Add("updated"));
            context.Lifecycle.OnBeforeUnmount(() => _lifecycle.Add("before-unmount"));
            context.Lifecycle.OnUnmounted(() => _lifecycle.Add("unmounted"));
            return () => ComponentTree.Element(
                "p",
                children: [ComponentTree.Text(_text.Value)]);
        }

        public void Dispose()
        {
            _lifecycle.Add("template-disposed");
        }
    }

    private sealed class ArgumentsTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("title", isRequired: true),
        ];

        internal int SetupCount { get; private set; }

        internal int RenderCount { get; private set; }

        internal int BeforeUpdateCount { get; private set; }

        internal int UpdatedCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            SetupCount++;
            context.Lifecycle.OnBeforeUpdate(() => BeforeUpdateCount++);
            context.Lifecycle.OnUpdated(() => UpdatedCount++);
            return () =>
            {
                RenderCount++;
                return ComponentTree.Element(
                    "h1",
                    children:
                    [
                        ComponentTree.Text(
                            context.Arguments.Get<string>("title") ?? string.Empty),
                    ]);
            };
        }
    }

    private sealed class EventTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent("save"),
        ];

        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                RenderCount++;
                return ComponentTree.Element("button");
            };
        }
    }

    private sealed class CapturingParentTemplate : IComponentTemplate
    {
        internal List<string> CapturedMessages { get; } = [];

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnErrorCaptured(
                (exception, _, _) =>
                {
                    CapturedMessages.Add(exception.Message);
                    return false;
                });
            return () => ComponentTree.Element(
                "section",
                children:
                [
                    ComponentTree.Template<ThrowingChildTemplate>(),
                ]);
        }
    }

    private sealed class LifecycleOrderTemplate : IComponentTemplate
    {
        private readonly List<string> _order;

        internal LifecycleOrderTemplate(List<string> order)
        {
            _order = order;
        }

        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("title", isRequired: true),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            _order.Add("setup");
            context.Lifecycle.OnBeforeMount(
                () => _order.Add("component-before-mount"));
            context.Lifecycle.OnMounted(
                () => _order.Add("component-mounted"));
            context.Lifecycle.OnBeforeUpdate(
                () => _order.Add("component-before-update"));
            context.Lifecycle.OnUpdated(
                () => _order.Add("component-updated"));
            context.Lifecycle.OnBeforeUnmount(
                () => _order.Add("component-before-unmount"));
            context.Lifecycle.OnUnmounted(
                () => _order.Add("component-unmounted"));
            return () => ComponentTree.Element(
                "p",
                children:
                [
                    ComponentTree.Text(
                        context.Arguments.Get<string>("title")
                        ?? string.Empty),
                ]);
        }
    }

    private sealed class ThrowingChildTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => throw new InvalidOperationException(
                "child render failed");
        }
    }

    private sealed class ThrowingBlockTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () =>
            {
                RenderHelpers._openBlock();
                throw new InvalidOperationException("mid-block boom");
            };
        }
    }

    private sealed class ItemTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("text", isRequired: true),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Element(
                "li",
                children:
                [
                    ComponentTree.Text(
                        context.Arguments.Get<string>("text") ?? string.Empty),
                ]);
        }
    }

    private sealed class ScopedTemplate : IComponentTemplate
    {
        public string? ScopeIdentifier => "data-viu-scoped";

        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element(
                "article",
                new ComponentAttributes(
                [
                    new ComponentAttribute("class", "owned"),
                ]));
        }
    }

    private sealed class CommitTemplate : IComponentTemplate
    {
        private readonly Action _observeCommit;
        private readonly Reference<string> _text;

        internal CommitTemplate(
            Reference<string> text,
            Action observeCommit)
        {
            _text = text;
            _observeCommit = observeCommit;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(_observeCommit);
            context.Lifecycle.OnUpdated(_observeCommit);
            return () => ComponentTree.Element(
                "output",
                children: [ComponentTree.Text(_text.Value)]);
        }
    }

    private sealed class HostAccessTemplate : IComponentTemplate
    {
        internal IReadOnlyList<FakeHostNode> RootElements { get; private set; } =
            Array.Empty<FakeHostNode>();

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(
                () => RootElements =
                    ComponentHost.GetRootElements<FakeHostNode>(context));
            return static () => ComponentTree.Fragment(
            [
                ComponentTree.Element(
                    "div",
                    children:
                    [
                        ComponentTree.Element("strong"),
                    ]),
                ComponentTree.Element("span"),
            ]);
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
