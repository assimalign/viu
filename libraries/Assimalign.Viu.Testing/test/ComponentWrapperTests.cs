using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Testing.Tests;

// Pins the component test utilities against @vue/test-utils (https://test-utils.vuejs.org/api/):
// mount, find/findComponent, trigger/setValue, emitted, stubs, and async flush semantics
// (https://test-utils.vuejs.org/guide/advanced/async-suspense.html). All DOM-free in xUnit.
public class ComponentWrapperTests
{
    [Fact]
    public void Mount_RendersComponent_ExposesHtmlTextAndInstance()
    {
        using var wrapper = ViuTest.Mount(new CounterComponent());

        wrapper.Instance.ShouldNotBeNull();
        wrapper.Exists().ShouldBeTrue();
        wrapper.Html().ShouldBe("<div class=\"counter\"><span class=\"count\">0</span><button>+</button></div>");
        wrapper.Text().ShouldBe("0+");
    }

    [Fact]
    public void Find_SupportsTagIdClassAndAttributeSelectors()
    {
        using var wrapper = ViuTest.Mount(new SelectorComponent());

        wrapper.Find("span").ShouldNotBeNull();
        wrapper.Find("#title").ShouldNotBeNull();
        wrapper.Find(".highlight").ShouldNotBeNull();
        wrapper.Find("[data-role=action]").ShouldNotBeNull();
        wrapper.Find(".missing").ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => { wrapper.Get(".missing"); });
        wrapper.FindAll("span").Count.ShouldBe(2);
    }

    [Fact]
    public void FindComponent_LocatesAChildByType_WithExistsAndThrowingVariants()
    {
        var host = new HostComponent();
        using var wrapper = ViuTest.Mount(host);

        var child = wrapper.FindComponent<CounterComponent>();
        child.ShouldNotBeNull();
        child.Instance.Definition.ShouldBeSameAs(host.Counter);

        wrapper.FindComponent<SelectorComponent>().ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => { wrapper.GetComponent<SelectorComponent>(); });
    }

    [Fact]
    public async Task Trigger_DispatchesThroughTheEventPath_AndAwaitsTheFlush()
    {
        using var wrapper = ViuTest.Mount(new CounterComponent());
        wrapper.Find(".count")!.Text().ShouldBe("0");

        await wrapper.Get("button").Trigger("click");

        // The awaited trigger completes only after the scheduler flush, so post-update state is
        // observable without a manual NextTick.
        wrapper.Find(".count")!.Text().ShouldBe("1");
    }

    [Fact]
    public async Task SetValue_UpdatesInput_FiresInput_AndAwaitsTheFlush()
    {
        using var wrapper = ViuTest.Mount(new InputComponent());

        await wrapper.Get("input").SetValue("hello");

        wrapper.Find(".echo")!.Text().ShouldBe("hello");
    }

    [Fact]
    public async Task Emitted_CapturesEventsInOrder_IncludingDuringMount()
    {
        using var wrapper = ViuTest.Mount(new CounterComponent());

        // Emitted during mount (the component emits "ready" in setup).
        wrapper.Emitted("ready").Count.ShouldBe(1);
        wrapper.Emitted("ready")[0].ShouldBe(new object?[] { 0 });

        await wrapper.Get("button").Trigger("click");
        await wrapper.Get("button").Trigger("click");

        var changes = wrapper.Emitted("change");
        changes.Count.ShouldBe(2);
        changes[0].ShouldBe(new object?[] { 1 });
        changes[1].ShouldBe(new object?[] { 2 });
        wrapper.Emitted().ShouldContainKey("change");
    }

    [Fact]
    public void Stub_ReplacesAChildComponent_WithARecognizablePlaceholderTag()
    {
        var host = new HostComponent();
        var options = new ComponentMountOptions().Stub(host.Counter);
        using var wrapper = ViuTest.Mount(host, options);

        // The real counter never renders — its <span class="count"> is absent, a <counter-stub> is
        // in its place (upstream default stub placeholder).
        wrapper.Html().ShouldContain("<counter-stub>");
        wrapper.Find(".count").ShouldBeNull();
        wrapper.FindComponent<CounterComponent>().ShouldBeNull();
    }

    [Fact]
    public void Provides_FromGlobalConfig_AreInjectableByTheComponent()
    {
        var options = new ComponentMountOptions().Provide("theme", "dark");
        using var wrapper = ViuTest.Mount(new InjectingComponent(), options);

        wrapper.Text().ShouldBe("dark");
    }

    [Fact]
    public async Task SelfHostedSuite_CounterComponent_DrivesAsyncStateThroughTheRealScheduler()
    {
        // A non-trivial component exercised end to end: props seed state, a click mutates a reactive
        // ref (scheduled re-render), and the awaited trigger observes the post-flush DOM and the
        // emitted event — no sleeping or polling.
        var options = new ComponentMountOptions { Properties = VirtualNodeFactory.Properties(("start", 10)) };
        using var wrapper = ViuTest.Mount(new CounterComponent(), options);
        wrapper.Get(".count").Text().ShouldBe("10");

        await wrapper.Get("button").Trigger("click");
        wrapper.Get(".count").Text().ShouldBe("11");

        await wrapper.Get("button").Trigger("click");
        wrapper.Get(".count").Text().ShouldBe("12");

        var changes = wrapper.Emitted("change");
        changes.Count.ShouldBe(2);
        changes[0].ShouldBe(new object?[] { 11 });
        changes[1].ShouldBe(new object?[] { 12 });
    }

    // --- sample components -----------------------------------------------------------------------

    // A counter: prop-seeded reactive count, emits "ready" on mount and "change" per increment.
    private sealed class CounterComponent : IComponentDefinition
    {
        private static readonly IReadOnlyList<ComponentPropertyDefinition> DeclaredProperties =
            [new ComponentPropertyDefinition("start") { DefaultValue = 0 }];
        private static readonly IReadOnlyList<ComponentEmitDefinition> DeclaredEmits =
            [new ComponentEmitDefinition("ready"), new ComponentEmitDefinition("change")];

        public string? Name => "Counter";

        public IReadOnlyList<ComponentPropertyDefinition>? Properties => DeclaredProperties;

        public IReadOnlyList<ComponentEmitDefinition>? Emits => DeclaredEmits;

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            var count = Reactive.Reference(properties.Get<int>("start"));
            context.Emit("ready", count.Value); // emitted during mount
            void Increment()
            {
                count.Value++;
                context.Emit("change", count.Value);
            }
            return () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "counter")),
                VirtualNodeFactory.Element(
                    "span", VirtualNodeFactory.Properties(("class", "count")), count.Value.ToString()),
                VirtualNodeFactory.Element(
                    "button", VirtualNodeFactory.Properties(("onClick", (Action)Increment)), "+"));
        }
    }

    // Renders a Counter child (stable definition) so FindComponent/stubbing have something to find.
    private sealed class HostComponent : IComponentDefinition
    {
        public CounterComponent Counter { get; } = new();

        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
            => () => VirtualNodeFactory.Element("main", VirtualNodeFactory.Component(Counter));
    }

    // Covers each selector kind (two spans, an id, a class, a data attribute).
    private sealed class SelectorComponent : IComponentDefinition
    {
        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
            => () => VirtualNodeFactory.Element(
                "section",
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("id", "title")), "Title"),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "highlight")), "Body"),
                VirtualNodeFactory.Element(
                    "button", VirtualNodeFactory.Properties(("data-role", "action")), "Go"));
    }

    // A v-model-style input echoing its value into a span.
    private sealed class InputComponent : IComponentDefinition
    {
        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            var text = Reactive.Reference(string.Empty);
            void OnInput(object? value) => text.Value = value?.ToString() ?? string.Empty;
            return () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Element(
                    "input",
                    VirtualNodeFactory.Properties(("onInput", (Action<object?>)OnInput), ("value", text.Value)),
                    (VirtualNode?[]?)null),
                VirtualNodeFactory.Element(
                    "span", VirtualNodeFactory.Properties(("class", "echo")), text.Value));
        }
    }

    // Injects an app-level provide and renders it.
    private sealed class InjectingComponent : IComponentDefinition
    {
        public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
        {
            var theme = DependencyInjection.Inject("theme") as string ?? "default";
            return () => VirtualNodeFactory.Element("div", theme);
        }
    }
}
