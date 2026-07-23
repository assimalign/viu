using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Tests;

public sealed class ComponentRuntimeTests
{
    [Theory]
    [InlineData(typeof(BaseTransition))]
    [InlineData(typeof(KeepAlive))]
    [InlineData(typeof(Suspense))]
    public void Create_CoreBuiltIn_DoesNotRequireApplicationFactoryRegistration(
        Type templateType)
    {
        ITemplateComponent request = new TemplateComponent(templateType);
        IApplicationContext application = new ApplicationContext(
            request,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider());

        MountedComponent mounted = MountedComponent.Create(
            application,
            request);

        mounted.Template.GetType().ShouldBe(templateType);
        mounted.AbortMount();
    }

    [Fact]
    public void Create_SetupRunsInsideOwnedReactiveScope_AndUnmountUsesDefinedOrder()
    {
        List<string> order = [];
        LifecycleTemplate template = new(order);
        MountedComponent mounted = CreateMounted(template);

        mounted.InvokeBeforeMount();
        mounted.InvokeMounted();
        mounted.Unmount(() => order.Add("subtree"));

        order.ShouldBe(
        [
            "setup",
            "before-mount",
            "mounted",
            "before-unmount",
            "scope-cleanup",
            "subtree",
            "unmounted-canceled:True",
            "template-disposed",
        ]);
    }

    [Fact]
    public async Task Emit_AsynchronousListenerFault_IsObservedByApplicationHandler()
    {
        TaskCompletionSource listener = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Exception> observed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventTemplate template = new();
        ITemplateComponent request = ComponentTree.Template<EventTemplate>(
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["save"] = new ComponentEventListener(_ => listener.Task),
            });
        MountedComponent mounted = CreateMounted(
            template,
            request,
            configure: context =>
                context.ErrorHandler = (exception, _, _) => observed.TrySetResult(exception));

        mounted.Context.Emit("save", 7);
        listener.SetException(new InvalidOperationException("save failed"));

        Exception exception = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        exception.Message.ShouldBe("save failed");
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void ChildError_AncestorCaptureReturningFalse_StopsApplicationPropagation()
    {
        int applicationErrors = 0;
        CapturingTemplate parentTemplate = new();
        EventTemplate childTemplate = new();
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(CapturingTemplate),
                () => parentTemplate),
            new ComponentRegistration(
                typeof(EventTemplate),
                () => childTemplate),
        ]);
        IApplicationContext application = new ApplicationContext(
            ComponentTree.Template<CapturingTemplate>(),
            factory,
            new EmptyServiceProvider())
        {
            ErrorHandler = (_, _, _) => applicationErrors++,
        };
        MountedComponent parent = MountedComponent.Create(
            application,
            ComponentTree.Template<CapturingTemplate>());
        ITemplateComponent childRequest = ComponentTree.Template<EventTemplate>(
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["save"] = new ComponentEventListener(
                    _ => throw new InvalidOperationException("captured")),
            });
        MountedComponent child = CreateMounted(
            childTemplate,
            childRequest,
            parent: parent.Context,
            application: application);

        child.Context.Emit("save");

        parentTemplate.CapturedMessages.ShouldBe(["captured"]);
        applicationErrors.ShouldBe(0);
        child.Unmount(static () => { });
        parent.Unmount(static () => { });
    }

    [Fact]
    public void Update_RefreshesLiveArgumentsSlotsListenersAndFallthroughAttributes()
    {
        ArgumentsTemplate template = new();
        ITemplateComponent initial = ComponentTree.Template<ArgumentsTemplate>(
            arguments: new ComponentArguments(
            [
                new KeyValuePair<string, object?>("title", "Initial"),
                new KeyValuePair<string, object?>("data-id", "first"),
            ]),
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => ComponentTree.Text("slot"),
            });
        MountedComponent mounted = CreateMounted(template, initial);

        mounted.Context.Arguments.Get<string>("title").ShouldBe("Initial");
        mounted.Context.Attributes.TryGetValue("data-id", out object? identifier).ShouldBeTrue();
        identifier.ShouldBe("first");
        mounted.Context.Slots.ContainsKey("default").ShouldBeTrue();

        mounted.Update(
            ComponentTree.Template<ArgumentsTemplate>(
                arguments: new ComponentArguments(
                [
                    new KeyValuePair<string, object?>("data-id", "second"),
                ])));

        mounted.Context.Arguments.Get<string>("title").ShouldBe("Default title");
        mounted.Context.Attributes.TryGetValue("data-id", out identifier).ShouldBeTrue();
        identifier.ShouldBe("second");
        mounted.Context.Slots.ShouldBeEmpty();
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Context_ExposesConfiguredStateRegistryThroughStateOwnedCapability()
    {
        EventTemplate template = new();
        IStateStoreRegistry state = new StateStoreRegistry(
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider(),
            new ReactiveEffectScopeFactory());
        MountedComponent mounted = CreateMounted(template, state: state);

        IStateStoreContext bridge = mounted.Context;

        bridge.State.ShouldBeSameAs(state);
        mounted.Unmount(static () => { });
        state.Dispose();
    }

    [Fact]
    public void Context_WarningCapability_ForwardsToApplicationWarningHandler()
    {
        List<string> warnings = [];
        EventTemplate template = new();
        MountedComponent mounted = CreateMounted(
            template,
            configure: context => context.WarnHandler = warnings.Add);

        IComponentWarningContext warningContext = mounted.Context;
        warningContext.Warn("host-neutral warning");

        warnings.ShouldBe(["host-neutral warning"]);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Emit_ApplicationDiagnosticObserver_SeesEventsWithoutAListener()
    {
        List<(string Name, IReadOnlyList<object?> Arguments)> observed = [];
        EventTemplate template = new();
        MountedComponent mounted = CreateMounted(
            template,
            configure: context =>
                ((ApplicationContext)context).EventObserver =
                    (_, name, arguments) => observed.Add((name, arguments)));

        mounted.Context.Emit("save", 42, "complete");

        observed.Count.ShouldBe(1);
        observed[0].Name.ShouldBe("save");
        observed[0].Arguments.ShouldBe([42, "complete"]);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Update_KebabParameterAlias_UsesCanonicalArgumentAndRunsValidator()
    {
        List<string> warnings = [];
        ParameterSemanticsTemplate template = new();
        ITemplateComponent request = ComponentTree.Template<ParameterSemanticsTemplate>(
            arguments: new ComponentArguments(
            [
                new KeyValuePair<string, object?>("item-count", -1),
            ]));
        MountedComponent mounted = CreateMounted(
            template,
            request,
            configure: context => context.WarnHandler = warnings.Add);

        mounted.Context.Arguments.Contains("itemCount").ShouldBeTrue();
        mounted.Context.Arguments.Get<int>("itemCount").ShouldBe(-1);
        mounted.Context.Attributes.TryGetValue("item-count", out _).ShouldBeFalse();
        warnings.Exists(
            warning => warning.Contains(
                "Invalid value was supplied",
                StringComparison.Ordinal)).ShouldBeTrue();

        mounted.Update(
            ComponentTree.Template<ParameterSemanticsTemplate>(
                arguments: new ComponentArguments(
                [
                    new KeyValuePair<string, object?>("itemCount", 2),
                ])));

        mounted.Context.Arguments.Get<int>("itemCount").ShouldBe(2);
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Emit_KebabEvent_DispatchesSpreadAndOnceListenersAcrossUpdates()
    {
        List<IReadOnlyList<object?>> regularArguments = [];
        int onceInvocationCount = 0;
        List<string> warnings = [];
        EventSemanticsTemplate template = new();
        ComponentEventListener regular =
            ComponentEventListener.ForArguments(regularArguments.Add);
        ComponentEventListener once =
            ComponentEventListener.ForArguments(_ => onceInvocationCount++);
        ITemplateComponent request = ComponentTree.Template<EventSemanticsTemplate>(
            arguments: new ComponentArguments(
            [
                new KeyValuePair<string, object?>(
                    "onItemSelected",
                    (Action<object?>)(_ => { })),
                new KeyValuePair<string, object?>(
                    "onItemSelectedOnce",
                    (Action<object?>)(_ => { })),
            ]),
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["itemSelected"] = regular,
                ["itemSelectedOnce"] = once,
            });
        MountedComponent mounted = CreateMounted(
            template,
            request,
            configure: context => context.WarnHandler = warnings.Add);

        mounted.Context.Attributes.TryGetValue("onItemSelected", out _).ShouldBeFalse();
        mounted.Context.Attributes.TryGetValue("onItemSelectedOnce", out _).ShouldBeFalse();
        mounted.Context.Emit("item-selected", 7, "first");
        mounted.Update(
            ComponentTree.Template<EventSemanticsTemplate>(
                listeners: new Dictionary<string, ComponentEventListener>
                {
                    ["itemSelected"] = regular,
                    ["itemSelectedOnce"] =
                        ComponentEventListener.ForArguments(
                            _ => onceInvocationCount += 10),
                }));
        mounted.Context.Emit("item-selected", 8, "second");
        mounted.Context.Emit("item-selected", 9);

        regularArguments.Count.ShouldBe(3);
        regularArguments[0].ShouldBe([7, "first"]);
        regularArguments[1].ShouldBe([8, "second"]);
        regularArguments[2].ShouldBe([9]);
        onceInvocationCount.ShouldBe(1);
        warnings.Exists(
            warning => warning.Contains(
                "Invalid arguments were emitted",
                StringComparison.Ordinal)).ShouldBeTrue();
        mounted.Unmount(static () => { });
    }

    [Fact]
    public void Render_Fallthrough_MergesClassStyleAndEventListeners()
    {
        List<string> invocationOrder = [];
        Action<object?> rootClick = _ => invocationOrder.Add("root");
        Action<object?> inheritedClick = _ => invocationOrder.Add("fallthrough");
        FallthroughTemplate template = new(rootClick);
        ITemplateComponent request = ComponentTree.Template<FallthroughTemplate>(
            arguments: new ComponentArguments(
            [
                new KeyValuePair<string, object?>("class", "extra"),
                new KeyValuePair<string, object?>(
                    "style",
                    new Dictionary<string, object?>
                    {
                        ["color"] = "blue",
                        ["margin"] = "2px",
                    }),
                new KeyValuePair<string, object?>("onClick", inheritedClick),
                new KeyValuePair<string, object?>(
                    "onSave",
                    (Action<object?>)(_ => { })),
            ]),
            listeners: new Dictionary<string, ComponentEventListener>
            {
                ["save"] = new ComponentEventListener(_ => { }),
            });
        MountedComponent mounted = CreateMounted(template, request);

        IElementComponent element =
            mounted.Render().ShouldBeAssignableTo<IElementComponent>();

        element.Attributes.TryGetValue("class", out object? cssClass).ShouldBeTrue();
        cssClass.ShouldBe("owned extra");
        element.Attributes.TryGetValue("style", out object? style).ShouldBeTrue();
        IReadOnlyDictionary<string, object?> styleValues =
            style.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        styleValues["color"].ShouldBe("blue");
        styleValues["padding"].ShouldBe("1px");
        styleValues["margin"].ShouldBe("2px");
        element.Attributes.TryGetValue("onSave", out _).ShouldBeFalse();
        element.Attributes.TryGetValue("onClick", out object? click).ShouldBeTrue();

        Action<object?> combinedClick =
            click.ShouldBeAssignableTo<Action<object?>>()!;
        combinedClick(null);

        invocationOrder.ShouldBe(["root", "fallthrough"]);
        mounted.Unmount(static () => { });
    }

    private static MountedComponent CreateMounted(
        IComponentTemplate template,
        ITemplateComponent? request = null,
        ComponentContext? parent = null,
        IApplicationContext? application = null,
        Action<IApplicationContext>? configure = null,
        IStateStoreRegistry? state = null)
    {
        Type templateType = template.GetType();
        ComponentFactory factory = new(
        [
            new ComponentRegistration(templateType, () => template),
        ]);
        IApplicationContext context = application
            ?? new ApplicationContext(
                request ?? ComponentTree.Template(templateType),
                factory,
                new EmptyServiceProvider(),
                state);
        configure?.Invoke(context);
        return MountedComponent.Create(
            context,
            request ?? ComponentTree.Template(templateType),
            parent);
    }

    private sealed class LifecycleTemplate : IComponentTemplate, IDisposable
    {
        private readonly List<string> _order;

        internal LifecycleTemplate(List<string> order)
        {
            _order = order;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            _order.Add("setup");
            Reactive.OnScopeDispose(() => _order.Add("scope-cleanup"));
            context.Lifecycle.OnBeforeMount(() => _order.Add("before-mount"));
            context.Lifecycle.OnMounted(() => _order.Add("mounted"));
            context.Lifecycle.OnBeforeUnmount(() => _order.Add("before-unmount"));
            context.Lifecycle.OnUnmounted(
                cancellationToken =>
                {
                    _order.Add($"unmounted-canceled:{cancellationToken.IsCancellationRequested}");
                    return Task.CompletedTask;
                });
            return () => ComponentTree.Element("main");
        }

        public void Dispose()
        {
            _order.Add("template-disposed");
        }
    }

    private sealed class EventTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent("save"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Comment();
        }
    }

    private sealed class CapturingTemplate : IComponentTemplate
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
            return () => ComponentTree.Comment();
        }
    }

    private sealed class ArgumentsTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("title", defaultFactory: () => "Default title"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Comment();
        }
    }

    private sealed class ParameterSemanticsTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter(
                "itemCount",
                validator: value => value is int count && count >= 0),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Comment();
        }
    }

    private sealed class EventSemanticsTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent(
                "itemSelected",
                arguments => arguments.Count == 2),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Comment();
        }
    }

    private sealed class FallthroughTemplate : IComponentTemplate
    {
        private readonly Action<object?> _rootClick;

        internal FallthroughTemplate(Action<object?> rootClick)
        {
            _rootClick = rootClick;
        }

        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent("save"),
        ];

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Element(
                "button",
                new ComponentAttributes(
                [
                    new ComponentAttribute("class", "owned"),
                    new ComponentAttribute(
                        "style",
                        new Dictionary<string, object?>
                        {
                            ["color"] = "red",
                            ["padding"] = "1px",
                        }),
                    new ComponentAttribute("onClick", _rootClick),
                ]));
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
