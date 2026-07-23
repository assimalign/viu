using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class ComponentWrapperTests
{
    [Fact]
    public void Mount_PrimitiveTree_ExposesMarkupTextAndDescription()
    {
        IComponent tree = ComponentTree.Element(
            "main",
            children:
            [
                ComponentTree.Element(
                    "h1",
                    children: [ComponentTree.Text("Title")]),
                ComponentTree.Text("Body"),
            ]);

        using ComponentWrapper wrapper = ViuTest.Mount(tree);

        wrapper.Component.ShouldBeSameAs(tree);
        wrapper.Exists().ShouldBeTrue();
        wrapper.Html().ShouldBe("<main><h1>Title</h1>Body</main>");
        wrapper.Text().ShouldBe("TitleBody");
    }

    [Fact]
    public void FindAndGet_QueryRenderedHostTree()
    {
        using ComponentWrapper wrapper = ViuTest.Mount(
            ComponentTree.Element(
                "section",
                children:
                [
                    ComponentTree.Element(
                        "span",
                        Attributes(("id", "title")),
                        [ComponentTree.Text("Title")]),
                    ComponentTree.Element(
                        "span",
                        Attributes(("class", "highlight")),
                        [ComponentTree.Text("Body")]),
                    ComponentTree.Element(
                        "button",
                        Attributes(("data-role", "action")),
                        [ComponentTree.Text("Go")]),
                ]));

        wrapper.Find("#title")!.Text().ShouldBe("Title");
        wrapper.Get(".highlight").Text().ShouldBe("Body");
        wrapper.Get("[data-role=action]").Text().ShouldBe("Go");
        wrapper.Find("missing").ShouldBeNull();
        wrapper.FindAll("span").Count.ShouldBe(2);
    }

    [Fact]
    public async Task Trigger_DispatchesSynchronousHostEventAndFlushesScheduler()
    {
        int clicks = 0;
        using ComponentWrapper wrapper = ViuTest.Mount(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", (Action)(() => clicks++))),
                [ComponentTree.Text("Increment")]));

        await wrapper.Trigger("click");

        clicks.ShouldBe(1);
    }

    [Fact]
    public async Task ElementTrigger_AwaitsTaskReturningHostEvent()
    {
        bool completed = false;
        async Task SaveAsync()
        {
            await Task.Yield();
            completed = true;
        }

        using ComponentWrapper wrapper = ViuTest.Mount(
            ComponentTree.Element(
                "button",
                Attributes(("onClick", (Func<Task>)SaveAsync)),
                [ComponentTree.Text("Save")]));

        await wrapper.Get("button").Trigger("click");

        completed.ShouldBeTrue();
    }

    [Fact]
    public async Task SetValue_UpdatesHostValueAndPassesInputPayload()
    {
        object? received = null;
        using ComponentWrapper wrapper = ViuTest.Mount(
            ComponentTree.Element(
                "input",
                Attributes(
                    ("value", "before"),
                    ("onInput", (Action<object?>)(value => received = value)))));
        ElementWrapper input = wrapper.Get("input");

        await input.SetValue("after");

        input.Attribute("value").ShouldBe("after");
        received.ShouldBe("after");
    }

    [Fact]
    public void Emitted_PrimitiveTree_HasNoTemplateEmissionSource()
    {
        using ComponentWrapper wrapper =
            ViuTest.Mount(ComponentTree.Element("div"));

        wrapper.Emitted().ShouldBeEmpty();
        wrapper.Emitted("saved").ShouldBeEmpty();
    }

    [Fact]
    public void FindComponent_ReturnsScopedChildWrapperAndGetThrowsWhenMissing()
    {
        ComponentMountOptions options = new()
        {
            Components = new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(ChildTemplate),
                    static () => new ChildTemplate()),
            ]),
        };
        using ComponentWrapper wrapper =
            ViuTest.Mount(new ParentTemplate(), options);

        ComponentWrapper? child = wrapper.FindComponent<ChildTemplate>();

        child.ShouldNotBeNull();
        child.Instance.ShouldBeOfType<ChildTemplate>();
        child.Context.ShouldNotBeNull();
        child.Component.Kind.ShouldBe(ComponentKind.Template);
        child.Exists().ShouldBeTrue();
        child.Html().ShouldBe("<strong>child</strong>");
        child.Text().ShouldBe("child");
        child.Find("main").ShouldBeNull();
        wrapper.GetComponent<ChildTemplate>().Context
            .ShouldBeSameAs(child.Context);
        wrapper.FindComponent<TestTemplate>().ShouldBeNull();
        Should.Throw<InvalidOperationException>(
            () => wrapper.GetComponent<TestTemplate>());

        wrapper.Unmount();
        child.Exists().ShouldBeFalse();
        child.Html().ShouldBeEmpty();
    }

    [Fact]
    public void Mount_Template_ExposesRootInstanceContextArgumentsServicesAndLifecycle()
    {
        ReactiveTemplate template = new();
        TestService service = new();
        TestServiceProvider services = new(service);
        ComponentMountOptions options = new()
        {
            Arguments = Arguments(("start", 4)),
            Services = services,
        };

        using ComponentWrapper wrapper = ViuTest.Mount(template, options);

        wrapper.Component.Kind.ShouldBe(ComponentKind.Template);
        wrapper.Instance.ShouldBeSameAs(template);
        wrapper.Context.ShouldBeSameAs(template.Context);
        wrapper.Context!.Services.ShouldBeSameAs(services);
        template.ResolvedService.ShouldBeSameAs(service);
        template.IsMounted.ShouldBeTrue();
        wrapper.Html().ShouldBe(
            "<div class=\"counter\"><span class=\"count\">4</span><button>+</button></div>");
        wrapper.Emitted("ready").Count.ShouldBe(1);
        wrapper.Emitted("ready")[0].ShouldBe(new object?[] { 4 });
    }

    [Fact]
    public async Task Trigger_TemplateEvent_PatchesReactiveTreeCapturesEmitAndInvokesListener()
    {
        ReactiveTemplate template = new();
        List<int> listenedValues = [];
        ComponentMountOptions options = new()
        {
            Arguments = Arguments(("start", 2)),
            Listeners = new Dictionary<string, ComponentEventListener>
            {
                ["change"] = new ComponentEventListener(
                    value => listenedValues.Add((int)value!)),
            },
        };
        using ComponentWrapper wrapper = ViuTest.Mount(template, options);

        await wrapper.Get("button").Trigger("click");

        wrapper.Get(".count").Text().ShouldBe("3");
        wrapper.Emitted("change").Count.ShouldBe(1);
        wrapper.Emitted("change")[0].ShouldBe(new object?[] { 3 });
        listenedValues.ShouldBe([3]);
    }

    [Fact]
    public async Task Emitted_CapturesUndeclaredEventsPerMountedTemplateContext()
    {
        ComponentMountOptions options = new()
        {
            Components = new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(EventChildTemplate),
                    static () => new EventChildTemplate()),
            ]),
        };
        using ComponentWrapper wrapper =
            ViuTest.Mount(new EventParentTemplate(), options);
        ComponentWrapper child =
            wrapper.GetComponent<EventChildTemplate>();

        wrapper.Emitted("ready").Count.ShouldBe(1);
        wrapper.Emitted("ready")[0].ShouldBe(
            new object?[] { "parent" });
        child.Emitted("ready").Count.ShouldBe(1);
        child.Emitted("ready")[0].ShouldBe(
            new object?[] { "child" });
        wrapper.Emitted("child-only").ShouldBeEmpty();
        child.Emitted("parent-only").ShouldBeEmpty();
        wrapper.Emitted().ShouldContainKey("parent-only");
        child.Emitted().ShouldContainKey("child-only");

        await child.Trigger("click");

        child.Emitted("clicked").Count.ShouldBe(1);
        child.Emitted("clicked")[0].ShouldBe(
            new object?[] { 7 });
        wrapper.Emitted("clicked").ShouldBeEmpty();
    }

    [Fact]
    public async Task Trigger_TemplateTaskHandler_AwaitsTaskBeforeReactiveFlush()
    {
        AsyncTemplate template = new();
        using ComponentWrapper wrapper = ViuTest.Mount(template);

        await wrapper.Trigger("click");

        template.HandlerCompleted.ShouldBeTrue();
        wrapper.Text().ShouldBe("1");
    }

    [Fact]
    public void Mount_Template_PassesSlotsAndDelegatesChildActivation()
    {
        ComponentFactory children = new(
        [
            new ComponentRegistration(
                typeof(ChildTemplate),
                static () => new ChildTemplate()),
        ]);
        ComponentMountOptions options = new()
        {
            Components = children,
            Slots = new Dictionary<string, ComponentSlot>
            {
                ["default"] = static _ => ComponentTree.Text("slot"),
            },
        };

        using ComponentWrapper wrapper =
            ViuTest.Mount(new ParentTemplate(), options);

        wrapper.Html().ShouldBe("<main>slot<strong>child</strong></main>");
    }

    [Fact]
    public void Mount_Template_AutomaticStubReplacesChildActivation()
    {
        ComponentMountOptions options = new();
        options.Stub<ChildTemplate>();

        using ComponentWrapper wrapper =
            ViuTest.Mount(new ParentTemplate(), options);

        wrapper.Html().ShouldBe(
            "<main><!----><child-template-stub></child-template-stub></main>");
    }

    [Fact]
    public void Mount_ComponentTreeWithOptions_ActivatesTemplateChildren()
    {
        IComponent tree = ComponentTree.Element(
            "main",
            children: [ComponentTree.Template<ChildTemplate>()]);
        ComponentMountOptions options = new()
        {
            Components = new ComponentFactory(
            [
                new ComponentRegistration(
                    typeof(ChildTemplate),
                    static () => new ChildTemplate()),
            ]),
        };

        using ComponentWrapper wrapper = ViuTest.Mount(tree, options);

        wrapper.Context.ShouldBeNull();
        wrapper.Html().ShouldBe("<main><strong>child</strong></main>");
    }

    [Fact]
    public void Mount_ComponentTreeWithOptions_ResolvesRuntimeDirective()
    {
        Directive directive = new()
        {
            Created = (element, binding, _, _) =>
            {
                TestElement testElement = (TestElement)element;
                testElement.Properties["data-marked"] = binding.Value;
            },
        };
        ComponentMountOptions options = new()
        {
            Directives = new DirectiveRegistry(
            [
                new KeyValuePair<string, IDirective>("mark", directive),
            ]),
        };
        IComponent tree = ComponentTree.Element(
            "input",
            directives:
            [
                new ComponentDirectiveBinding("mark", "yes"),
            ]);

        using ComponentWrapper wrapper = ViuTest.Mount(tree, options);

        wrapper.Get("input").Attribute("data-marked").ShouldBe("yes");
    }

    [Fact]
    public void Mount_Template_StampsCompilerScopeIdentifier()
    {
        using ComponentWrapper wrapper =
            ViuTest.Mount(new ScopedTemplate());

        wrapper.Html().ShouldBe(
            "<section data-testing-scope=\"\"></section>");
    }

    [Fact]
    public void Mount_Template_UsesApplicationStateAndBorrowsRegistry()
    {
        TestServiceProvider services = new(null);
        ComponentFactory components =
            new(Array.Empty<ComponentRegistration>());
        using StateStoreRegistry state = StateStores.CreateRegistry(
            components,
            services,
            new ReactiveEffectScopeFactory());
        ComponentMountOptions options = new()
        {
            State = state,
        };

        using (ComponentWrapper wrapper =
            ViuTest.Mount(new StateTemplate(), options))
        {
            wrapper.Text().ShouldBe("application-state");
        }

        state.IsDisposed.ShouldBeFalse();
        state.Count.ShouldBe(1);
    }

    [Fact]
    public void Unmount_Template_RunsLifecycleAndDisposesMountOwnedInstance()
    {
        DisposableTemplate template = new();
        ComponentWrapper wrapper = ViuTest.Mount(template);

        wrapper.Unmount();

        wrapper.Exists().ShouldBeFalse();
        template.LifecycleEvents.ShouldBe(
        [
            "mounted",
            "before-unmount",
            "unmounted",
            "disposed",
        ]);
        wrapper.Dispose();
    }

    [Fact]
    public async Task NextTickAsync_DrivesCapturedSchedulerWork()
    {
        using ComponentWrapper wrapper =
            ViuTest.Mount(ComponentTree.Element("div"));
        bool ran = false;
        Scheduler.QueueJob(new SchedulerJob(() => ran = true));

        await wrapper.NextTickAsync();

        ran.ShouldBeTrue();
    }

    [Fact]
    public void Unmount_UpdatesWrapperExistence()
    {
        using ComponentWrapper wrapper =
            ViuTest.Mount(ComponentTree.Element("div"));

        wrapper.Unmount();

        wrapper.Exists().ShouldBeFalse();
        wrapper.Html().ShouldBeEmpty();
    }

    private static ComponentAttributes Attributes(
        params (string Name, object? Value)[] values)
    {
        List<IComponentAttribute> attributes = new(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            attributes.Add(
                new ComponentAttribute(
                    values[index].Name,
                    values[index].Value));
        }

        return new ComponentAttributes(attributes);
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

    private sealed class TestTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => ComponentTree.Element("div");
        }
    }

    private sealed class ReactiveTemplate : IComponentTemplate
    {
        public IReadOnlyList<IComponentParameter> Parameters { get; } =
        [
            new ComponentParameter("start", defaultFactory: static () => 0),
        ];

        public IReadOnlyList<IComponentEvent> Events { get; } =
        [
            new ComponentEvent("ready"),
            new ComponentEvent("change"),
        ];

        internal IComponentContext? Context { get; private set; }

        internal TestService? ResolvedService { get; private set; }

        internal bool IsMounted { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            Context = context;
            ResolvedService = context.Services.GetService(typeof(TestService))
                as TestService;
            Reference<int> count = Reactive.Reference(
                context.Arguments.Get<int>("start"));
            context.Lifecycle.OnMounted(() => IsMounted = true);
            context.Emit("ready", count.Value);

            void Increment()
            {
                count.Value++;
                context.Emit("change", count.Value);
            }

            return () => ComponentTree.Element(
                "div",
                Attributes(("class", "counter")),
                [
                    ComponentTree.Element(
                        "span",
                        Attributes(("class", "count")),
                        [ComponentTree.Text(count.Value.ToString())]),
                    ComponentTree.Element(
                        "button",
                        Attributes(("onClick", (Action)Increment)),
                        [ComponentTree.Text("+")]),
                ]);
        }
    }

    private sealed class AsyncTemplate : IComponentTemplate
    {
        internal bool HandlerCompleted { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            Reference<int> count = Reactive.Reference(0);

            async Task IncrementAsync()
            {
                await Task.Yield();
                count.Value++;
                HandlerCompleted = true;
            }

            return () => ComponentTree.Element(
                "button",
                Attributes(("onClick", (Func<Task>)IncrementAsync)),
                [ComponentTree.Text(count.Value.ToString())]);
        }
    }

    private sealed class ParentTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                IComponent slot = context.Slots.TryGetValue(
                    "default",
                    out ComponentSlot? content)
                        ? content(new ComponentArguments())
                            ?? ComponentTree.Comment()
                        : ComponentTree.Comment();
                return ComponentTree.Element(
                    "main",
                    children:
                    [
                        slot,
                        ComponentTree.Template<ChildTemplate>(),
                    ]);
            };
        }
    }

    private sealed class ChildTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element(
                "strong",
                children: [ComponentTree.Text("child")]);
        }
    }

    private sealed class EventParentTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Emit("ready", "parent");
            context.Emit("parent-only", true);
            return static () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Template<EventChildTemplate>(),
                ]);
        }
    }

    private sealed class EventChildTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Emit("ready", "child");
            context.Emit("child-only", true);
            void Click()
            {
                context.Emit("clicked", 7);
            }

            return () => ComponentTree.Element(
                "button",
                Attributes(("onClick", (Action)Click)),
                [ComponentTree.Text("child")]);
        }
    }

    private sealed class StateTemplate : IComponentTemplate
    {
        private static readonly StateStoreDefinition<TestState> State =
            StateStores.Define(
                "testing-state",
                static () => new TestState("application-state"));

        public ComponentRenderer Setup(IComponentContext context)
        {
            TestState state = State.Use(context);
            return () => ComponentTree.Element(
                "p",
                children: [ComponentTree.Text(state.Value)]);
        }
    }

    private sealed record TestState(string Value);

    private sealed class ScopedTemplate : IComponentTemplate
    {
        public string? ScopeIdentifier => "data-testing-scope";

        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element("section");
        }
    }

    private sealed class DisposableTemplate : IComponentTemplate, IDisposable
    {
        internal List<string> LifecycleEvents { get; } = [];

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(() => LifecycleEvents.Add("mounted"));
            context.Lifecycle.OnBeforeUnmount(
                () => LifecycleEvents.Add("before-unmount"));
            context.Lifecycle.OnUnmounted(
                () => LifecycleEvents.Add("unmounted"));
            return static () => ComponentTree.Element("div");
        }

        public void Dispose()
        {
            LifecycleEvents.Add("disposed");
        }
    }

    private sealed class TestService
    {
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly TestService? _service;

        internal TestServiceProvider(TestService? service)
        {
            _service = service;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(TestService)
                ? _service
                : null;
        }
    }
}
