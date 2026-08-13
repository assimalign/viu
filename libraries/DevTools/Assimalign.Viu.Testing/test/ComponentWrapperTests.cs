using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class ComponentWrapperTests
{
    [Fact]
    public void Mount_SuppliedInstance_QueriesRenderedHostRange()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new RootComponent());

        wrapper.Exists().ShouldBeTrue();
        wrapper.Instance.ShouldBeOfType<RootComponent>();
        wrapper.Context.ShouldNotBeNull();
        wrapper.Html().ShouldBe(
            "<main id=\"root\" class=\"shell\"><span>hello</span></main>");
        wrapper.Text().ShouldBe("hello");
        wrapper.Get("#root").Attribute("class").ShouldBe("shell");
        wrapper.FindAll("span").Count.ShouldBe(1);
    }

    [Fact]
    public void Mount_OneArgumentOverloads_UseDefaultApplicationComposition()
    {
        using (ComponentWrapper nodeWrapper = ComponentTest.Mount(
            new ElementNode(
                new QualifiedName("p"),
                children: [new TextNode("node")])))
        {
            nodeWrapper.Text().ShouldBe("node");
        }

        using (ComponentWrapper componentWrapper = ComponentTest.Mount(new RootComponent()))
        {
            componentWrapper.Text().ShouldBe("hello");
        }

        ComponentRegistration registration = new(
            ComponentReference.ForType(typeof(RootComponent)),
            new ComponentContract(displayName: nameof(RootComponent)),
            static _ => new RootComponent());
        using ComponentWrapper registrationWrapper = ComponentTest.Mount(registration);
        registrationWrapper.Text().ShouldBe("hello");
    }

    [Fact]
    public void FindComponent_TypeAndAncestry_ScopesChildHostRange()
    {
        ComponentFactory descendants = new();
        ComponentReference childReference = ComponentReference.ForType(typeof(ChildComponent));
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(displayName: "Child"),
                _ => new ChildComponent()));
        ComponentMountOptions options = new()
        {
            Components = descendants,
        };
        using ComponentWrapper root = ComponentTest.Mount(
            new ParentComponent(childReference),
            options);

        ComponentWrapper child = root.GetComponent<ChildComponent>();

        child.Instance.ShouldBeOfType<ChildComponent>();
        child.Context.ShouldNotBeNull().Parent.ShouldBeSameAs(root.Context);
        child.Html().ShouldBe("<section>child</section>");
        child.Text().ShouldBe("child");
        root.Html().ShouldBe("<main><section>child</section><footer>tail</footer></main>");
    }

    [Fact]
    public void ChildWrapper_RootUnmount_StableViewIdentityReportsNotMounted()
    {
        ComponentFactory descendants = CreateChildFactory(out ComponentReference childReference);
        using ComponentWrapper root = ComponentTest.Mount(
            new ParentComponent(childReference),
            new ComponentMountOptions { Components = descendants });
        ComponentWrapper child = root.GetComponent<ChildComponent>();
        child.Exists().ShouldBeTrue();

        root.Unmount();

        root.Exists().ShouldBeFalse();
        child.Exists().ShouldBeFalse();
        child.Html().ShouldBe(string.Empty);
    }

    [Fact]
    public void EventObserver_RootAndChildEmissions_AreCapturedPerContext()
    {
        ComponentReference childReference = ComponentReference.ForType(
            typeof(EmittingChildComponent));
        ComponentFactory descendants = new();
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(
                    displayName: "EmittingChild",
                    events: new[] { new ComponentEvent("child-ready") }),
                _ => new EmittingChildComponent()));
        ComponentMountOptions options = new()
        {
            Components = descendants,
            RootContract = new ComponentContract(
                displayName: "EmittingRoot",
                events: new[] { new ComponentEvent("root-ready") }),
        };
        using ComponentWrapper root = ComponentTest.Mount(
            new EmittingRootComponent(childReference),
            options);
        ComponentWrapper child = root.GetComponent<EmittingChildComponent>();

        root.Emitted("root-ready").ShouldHaveSingleItem()[0].ShouldBe("root");
        root.Emitted("child-ready").ShouldBeEmpty();
        child.Emitted("child-ready").ShouldHaveSingleItem()[0].ShouldBe("child");
        child.Emitted("root-ready").ShouldBeEmpty();
    }

    [Fact]
    public void EventObserver_ConfiguredObserver_IsPreservedAlongsideCapture()
    {
        int configuredObservations = 0;
        ComponentMountOptions options = new()
        {
            RootContract = new ComponentContract(
                displayName: "Observed",
                events: new[] { new ComponentEvent("ready") }),
            ConfigureApplication = application =>
            {
                application.EventObserver = (_, name, _) =>
                {
                    if (name == "ready")
                    {
                        configuredObservations++;
                    }
                };
            },
        };

        using ComponentWrapper wrapper = ComponentTest.Mount(new ObservedComponent(), options);

        configuredObservations.ShouldBe(1);
        wrapper.Emitted("ready").Count.ShouldBe(1);
    }

    [Fact]
    public void Stub_DescendantType_RendersGeneratedPlaceholder()
    {
        ComponentReference childReference = ComponentReference.ForType(typeof(ChildComponent));
        ComponentMountOptions options = new();
        options.Stub<ChildComponent>();

        using ComponentWrapper wrapper = ComponentTest.Mount(
            new ParentComponent(childReference),
            options);

        wrapper.Html().ShouldContain("<child-component-stub>");
        wrapper.FindComponent<ChildComponent>().ShouldBeNull();
    }

    [Fact]
    public async Task TriggerAsync_ReactiveListener_DrainsDeterministicScheduler()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new InteractiveComponent());
        wrapper.Text().ShouldBe("0");

        await wrapper.TriggerAsync("click");

        wrapper.Text().ShouldBe("1");
    }

    [Fact]
    public async Task SetValueAsync_ComponentAndElementWrappers_DispatchInputAndDrainScheduler()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new InputComponent());

        await wrapper.SetValueAsync("component");
        wrapper.Text().ShouldBe("component");

        ElementWrapper element = wrapper.Get("input");
        await element.SetValueAsync("element");
        wrapper.Text().ShouldBe("element");
    }

    [Fact]
    public async Task SetValueAsync_PortableElementEvent_DeliversInputValueThroughOneHandlerShape()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new PortableInputComponent());
        ElementWrapper element = wrapper.Get("input");

        await element.SetValueAsync("portable");

        PortableInputComponent component = wrapper.Instance.ShouldBeOfType<PortableInputComponent>();
        TestElementEvent payload = component.LastEvent.ShouldBeOfType<TestElementEvent>();
        payload.EventName.ShouldBe("input");
        payload.TargetValue.ShouldBe("portable");
        wrapper.Text().ShouldBe("portable");
    }

    [Fact]
    public async Task TriggerAsync_PortableElementEvent_PreservesPayloadIdentityAndFields()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new PortableKeyboardComponent());
        ElementWrapper element = wrapper.Get("input");
        TestElementEvent payload = new(
            "keydown",
            targetValue: "draft",
            key: "Enter",
            targetChecked: true,
            selectedValues: ["first", "second"]);

        await element.TriggerAsync("keydown", payload);

        PortableKeyboardComponent component =
            wrapper.Instance.ShouldBeOfType<PortableKeyboardComponent>();
        component.LastEvent.ShouldBeSameAs(payload);
        component.LastEvent!.TargetValue.ShouldBe("draft");
        component.LastEvent.Key.ShouldBe("Enter");
        component.LastEvent.TargetChecked.ShouldBeTrue();
        component.LastEvent.SelectedValues.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task TriggerAsync_BrowserModifierHandler_UsesExactConcretePayloadWithoutReflection()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new BrowserModifierComponent());
        BrowserEvent rejected = BrowserEventPayload(BrowserEventModifiers.None);
        BrowserEvent accepted = BrowserEventPayload(BrowserEventModifiers.Control);

        await wrapper.TriggerAsync("click", rejected);
        await wrapper.TriggerAsync("click", accepted);

        BrowserModifierComponent component =
            wrapper.Instance.ShouldBeOfType<BrowserModifierComponent>();
        component.InvocationCount.ShouldBe(1);
        component.LastEvent.ShouldBeSameAs(accepted);
    }

    [Fact]
    public async Task TriggerAsync_BrowserKeyGuard_UsesConcreteKeyboardPayload()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new BrowserKeyComponent());
        BrowserEvent rejected = BrowserEventPayload(eventName: "keydown", key: "Escape");
        BrowserEvent accepted = BrowserEventPayload(eventName: "keydown", key: "Enter");

        await wrapper.TriggerAsync("keydown", rejected);
        await wrapper.TriggerAsync("keydown", accepted);

        BrowserKeyComponent component =
            wrapper.Instance.ShouldBeOfType<BrowserKeyComponent>();
        component.InvocationCount.ShouldBe(1);
        component.LastEvent.ShouldBeSameAs(accepted);
    }

    [Fact]
    public async Task TriggerAsync_OnceCapturePassiveBinding_InvokesOnlyTheFirstPayload()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new OncePortableEventComponent());
        TestElementEvent first = new("click");
        TestElementEvent second = new("click");

        await wrapper.TriggerAsync("click", first);
        await wrapper.TriggerAsync("click", second);

        OncePortableEventComponent component =
            wrapper.Instance.ShouldBeOfType<OncePortableEventComponent>();
        component.InvocationCount.ShouldBe(1);
        component.LastEvent.ShouldBeSameAs(first);
        wrapper.Get("button").Element.EventListeners.ContainsKey("click").ShouldBeFalse();
    }

    [Fact]
    public async Task TriggerAsync_AwaitedHandlerContinuation_UsesComponentTestContextAndFlushes()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new AsynchronousPortableEventComponent());
        AsynchronousPortableEventComponent component =
            wrapper.Instance.ShouldBeOfType<AsynchronousPortableEventComponent>();

        await wrapper.TriggerAsync("click", new TestElementEvent("click"));

        component.Order.ShouldBe(["start", "resume"]);
        wrapper.Text().ShouldBe("1");
    }

    [Fact]
    public async Task TriggerAsync_ExplicitObjectPayloadOverloads_PreserveThePayloadValue()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new InputComponent());

        await wrapper.TriggerAsync("input", (object?)"component");
        wrapper.Text().ShouldBe("component");

        ElementWrapper element = wrapper.Get("input");
        await element.TriggerAsync("input", (object?)"element");
        wrapper.Text().ShouldBe("element");
    }

    [Fact]
    public async Task TriggerAsync_HandlerWithoutRunnableContinuation_FailsInsteadOfHanging()
    {
        using ComponentWrapper wrapper = ComponentTest.Mount(new BlockedPortableEventComponent());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => wrapper.TriggerAsync("click", new TestElementEvent("click")));

        exception.Message.ShouldContain("has no queued continuation");
        exception.Message.ShouldContain("does not wait on wall-clock or thread-pool timing");
    }

    [Fact]
    public void Mount_InvocationArguments_AreContractResolvedForSuppliedInstance()
    {
        ComponentMountOptions options = new()
        {
            RootContract = new ComponentContract(
                displayName: "Parameter",
                parameters: new[] { new ComponentParameter("message") }),
            Arguments = new Dictionary<string, object?>
            {
                ["message"] = "configured",
            },
        };

        using ComponentWrapper wrapper = ComponentTest.Mount(new ParameterComponent(), options);

        wrapper.Text().ShouldBe("configured");
    }

    private static ComponentFactory CreateChildFactory(out ComponentReference childReference)
    {
        childReference = ComponentReference.ForType(typeof(ChildComponent));
        ComponentFactory descendants = new();
        descendants.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(displayName: "Child"),
                _ => new ChildComponent()));
        return descendants;
    }

    private static BrowserEvent BrowserEventPayload(
        BrowserEventModifiers modifiers = BrowserEventModifiers.None,
        string eventName = "click",
        string key = "") =>
        new(
            eventName,
            100,
            key,
            string.Empty,
            modifiers,
            0,
            0,
            0,
            0,
            1,
            true,
            null,
            false);

    private sealed class RootComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("main"),
                bindings:
                [
                    ElementBinding.Attribute(new QualifiedName("id"), "root"),
                    ElementBinding.Attribute(new QualifiedName("class"), "shell"),
                ],
                children:
                [
                    new ElementNode(
                        new QualifiedName("span"),
                        children: new[] { new TextNode("hello") }),
                ]);
        }
    }

    private sealed class ParentComponent : IComponent
    {
        private readonly ComponentReference _childReference;

        internal ParentComponent(ComponentReference childReference)
        {
            _childReference = childReference;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("main"),
                children:
                [
                    new ComponentNode(_childReference),
                    new ElementNode(
                        new QualifiedName("footer"),
                        children: new[] { new TextNode("tail") }),
                ]);
        }
    }

    private sealed class ChildComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static _ => new ElementNode(
                new QualifiedName("section"),
                children: new[] { new TextNode("child") });
        }
    }

    private sealed class EmittingRootComponent : IComponent
    {
        private readonly ComponentReference _childReference;

        internal EmittingRootComponent(ComponentReference childReference)
        {
            _childReference = childReference;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("root-ready", "root");
            return _ => new ElementNode(
                new QualifiedName("main"),
                children: new[] { new ComponentNode(_childReference) });
        }
    }

    private sealed class EmittingChildComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("child-ready", "child");
            return static _ => new ElementNode(new QualifiedName("aside"));
        }
    }

    private sealed class ObservedComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Emit("ready");
            return static _ => new ElementNode(new QualifiedName("output"));
        }
    }

    private sealed class InteractiveComponent : IComponent
    {
        private readonly Reference<int> _count = Reactive.Reference(0);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    ElementBinding.Event("click", (Action)(() => _count.Value++)),
                ],
                children: new[] { new TextNode(_count.Value.ToString()) });
        }
    }

    private sealed class InputComponent : IComponent
    {
        private readonly Reference<object?> _value = Reactive.Reference<object?>(string.Empty);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("input"),
                bindings:
                [
                    ElementBinding.Property("value", _value.Value),
                    ElementBinding.Event("input", (Action<object?>)(value => _value.Value = value)),
                ],
                children: new[] { new TextNode(_value.Value?.ToString() ?? string.Empty) });
        }
    }

    private sealed class PortableInputComponent : IComponent
    {
        private readonly Reference<string> _value = Reactive.Reference(string.Empty);

        internal IElementEvent? LastEvent { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("input"),
                bindings:
                [
                    ElementBinding.Property("value", _value.Value),
                    ElementBinding.Event(
                        "input",
                        (Action<IElementEvent>)(elementEvent =>
                        {
                            LastEvent = elementEvent;
                            _value.Value = elementEvent.TargetValue ?? string.Empty;
                        })),
                ],
                children: [new TextNode(_value.Value)]);
        }
    }

    private sealed class PortableKeyboardComponent : IComponent
    {
        internal IElementEvent? LastEvent { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("input"),
                bindings:
                [
                    ElementBinding.Event(
                        "keydown",
                        (Action<IElementEvent>)(elementEvent => LastEvent = elementEvent)),
                ]);
        }
    }

    private sealed class BrowserModifierComponent : IComponent
    {
        internal int InvocationCount { get; private set; }

        internal BrowserEvent? LastEvent { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Action<BrowserEvent> listener = BrowserEvents.WithModifiers(
                browserEvent =>
                {
                    InvocationCount++;
                    LastEvent = browserEvent;
                },
                "ctrl",
                "exact");
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings: [ElementBinding.Event("click", listener)]);
        }
    }

    private sealed class BrowserKeyComponent : IComponent
    {
        internal int InvocationCount { get; private set; }

        internal BrowserEvent? LastEvent { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Action<BrowserEvent> listener = BrowserEvents.WithKeys(
                browserEvent =>
                {
                    InvocationCount++;
                    LastEvent = browserEvent;
                },
                "enter");
            return _ => new ElementNode(
                new QualifiedName("input"),
                bindings: [ElementBinding.Event("keydown", listener)]);
        }
    }

    private sealed class OncePortableEventComponent : IComponent
    {
        internal int InvocationCount { get; private set; }

        internal IElementEvent? LastEvent { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    ElementBinding.Event(
                        "clickCapturePassiveOnce",
                        (Action<IElementEvent>)(elementEvent =>
                        {
                            InvocationCount++;
                            LastEvent = elementEvent;
                        })),
                ]);
        }
    }

    private sealed class AsynchronousPortableEventComponent : IComponent
    {
        private readonly Reference<int> _count = Reactive.Reference(0);

        internal List<string> Order { get; } = [];

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    ElementBinding.Event(
                        "click",
                        (Func<IElementEvent, Task>)HandleAsync),
                ],
                children: [new TextNode(_count.Value.ToString())]);
        }

        private async Task HandleAsync(IElementEvent elementEvent)
        {
            _ = elementEvent;
            Order.Add("start");
            await Task.Yield();
            Order.Add("resume");
            _count.Value++;
        }
    }

    private sealed class BlockedPortableEventComponent : IComponent
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => new ElementNode(
                new QualifiedName("button"),
                bindings:
                [
                    ElementBinding.Event(
                        "click",
                        (Func<IElementEvent, Task>)(_ => _completion.Task)),
                ]);
        }
    }

    private sealed class ParameterComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context)
        {
            object? value = context.Bindings.Parameters["message"];
            return _ => new ElementNode(
                new QualifiedName("p"),
                children: new[] { new TextNode(value?.ToString() ?? string.Empty) });
        }
    }
}
