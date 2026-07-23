using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the props subsystem of @vue/runtime-core's componentProps.ts —
// https://vuejs.org/guide/components/props.html and /guide/components/attrs.html.
public class ComponentPropertiesTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ComponentPropertiesTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    private void Mount(IComponent component, VirtualNodeProperties? properties = null)
        => _renderer.Render(VirtualNodeFactory.Component(component, properties), _container);

    [Fact]
    public void DeclaredProps_ResolveWithCamelAndKebabCaseEquivalence()
    {
        string? camelSeen = null;
        string? kebabSeen = null;
        var component = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("modelValue"), new ComponentPropertyDefinition("itemCount")],
            SetupFunction = (properties, _) => () =>
            {
                camelSeen = (string?)properties["modelValue"];
                kebabSeen = (string?)properties["itemCount"];
                return VirtualNodeFactory.Text("x");
            },
        };

        // One prop passed camelCase, the other kebab-case: both resolve to the declaration.
        Mount(component, VirtualNodeFactory.Properties(("modelValue", "camel"), ("item-count", "kebab")));

        camelSeen.ShouldBe("camel");
        kebabSeen.ShouldBe("kebab");
    }

    [Fact]
    public void UndeclaredProps_LandInAttrs_AndFallThroughToASingleElementRoot()
    {
        var component = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("declared")],
            SetupFunction = static (_, _) => static () =>
                VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", "own")), "content"),
        };

        Mount(component, VirtualNodeFactory.Properties(
            ("declared", "value"),
            ("data-testid", "widget"),
            ("class", "extra")));

        // data-testid and class fall through; class merges with the root's own class.
        var root = (TestElement)_container.Children[0];
        root.Properties["data-testid"].ShouldBe("widget");
        root.Properties["class"].ShouldBe("own extra");
        root.Properties.ContainsKey("declared").ShouldBeFalse();
    }

    [Fact]
    public void InheritAttributesFalse_DisablesFallthrough_ButKeepsContextAttrs()
    {
        ComponentAttributes? seenAttributes = null;
        var component = new TestComponent
        {
            InheritAttributes = false,
            SetupFunction = (_, context) =>
            {
                seenAttributes = context.Attributes;
                return static () => VirtualNodeFactory.Element("div", "content");
            },
        };

        Mount(component, VirtualNodeFactory.Properties(("data-x", "1")));

        ((TestElement)_container.Children[0]).Properties.ContainsKey("data-x").ShouldBeFalse();
        seenAttributes!["data-x"].ShouldBe("1"); // still visible on context.Attributes
    }

    [Fact]
    public void Attrs_AreLive_UpdatedOnParentPatch()
    {
        ComponentAttributes? attributes = null;
        var child = new TestComponent
        {
            SetupFunction = (_, context) =>
            {
                attributes = context.Attributes;
                return static () => VirtualNodeFactory.Element("div", "x");
            },
        };
        var toggle = Reactive.Reference("a");
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "section",
                VirtualNodeFactory.Component(child, VirtualNodeFactory.Properties(("data-mode", toggle.Value)))),
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        attributes!["data-mode"].ShouldBe("a");

        toggle.Value = "b";
        _pump.RunUntilIdle();

        attributes["data-mode"].ShouldBe("b"); // the same live object reflects the patch
    }

    [Fact]
    public void Defaults_Apply_WithFactoryDefaultsPerInstance()
    {
        var factoryRuns = 0;
        List<string>? firstList = null;
        List<string>? secondList = null;
        var component = new TestComponent
        {
            Properties =
            [
                new ComponentPropertyDefinition("label") { DefaultValue = "fallback" },
                new ComponentPropertyDefinition("items")
                {
                    DefaultFactory = () =>
                    {
                        factoryRuns++;
                        return new List<string>();
                    },
                },
            ],
            SetupFunction = (properties, _) => () =>
            {
                var list = (List<string>?)properties["items"];
                if (firstList is null)
                {
                    firstList = list;
                }
                else
                {
                    secondList ??= list;
                }
                return VirtualNodeFactory.Element("div", (string?)properties["label"] ?? string.Empty);
            },
        };

        Mount(component);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>fallback</div></root>");
        factoryRuns.ShouldBe(1);

        var secondContainer = _renderer.CreateContainer();
        _renderer.Render(VirtualNodeFactory.Component(component), secondContainer);

        factoryRuns.ShouldBe(2); // one fresh default instance per component instance
        secondList.ShouldNotBeSameAs(firstList);
    }

    [Fact]
    public void RequiredAndValidatorViolations_WarnNamingComponentAndProp()
    {
        using var warnings = new WarningCapture();
        var component = new TestComponent
        {
            Name = "PriceTag",
            Properties =
            [
                new ComponentPropertyDefinition("amount") { Required = true },
                new ComponentPropertyDefinition("currency") { Validator = value => value is "USD" or "EUR" },
            ],
            SetupFunction = static (_, _) => static () => VirtualNodeFactory.Text("x"),
        };

        Mount(component, VirtualNodeFactory.Properties(("currency", "XYZ")));

        warnings.Messages.ShouldContain(message =>
            message.Contains("Missing required prop") && message.Contains("amount") && message.Contains("PriceTag"));
        warnings.Messages.ShouldContain(message =>
            message.Contains("custom validator") && message.Contains("currency") && message.Contains("PriceTag"));
    }

    [Fact]
    public void MutatingAPropFromTheChild_WarnsOneWayDataFlow()
    {
        using var warnings = new WarningCapture();
        ComponentProperties? captured = null;
        var component = new TestComponent
        {
            Name = "Readonly",
            Properties = [new ComponentPropertyDefinition("value")],
            SetupFunction = (properties, _) =>
            {
                captured = properties;
                return static () => VirtualNodeFactory.Text("x");
            },
        };

        Mount(component, VirtualNodeFactory.Properties(("value", 1)));
        captured!.Set("value", 2);

        warnings.Messages.ShouldContain(message =>
            message.Contains("mutate prop") && message.Contains("value") && message.Contains("Readonly"));
        captured["value"].ShouldBe(1); // the write was ignored
    }

    [Fact]
    public void PropReads_AreShallowReactive_OnlyReadersReRender()
    {
        var renders = 0;
        var child = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("used"), new ComponentPropertyDefinition("unused")],
            SetupFunction = (properties, _) => () =>
            {
                renders++;
                return VirtualNodeFactory.Element("span", (string?)properties["used"] ?? string.Empty);
            },
        };
        var used = Reactive.Reference("u1");
        var unused = Reactive.Reference("x1");
        var parent = new TestComponent
        {
            SetupFunction = (_, _) => () => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Component(child, VirtualNodeFactory.Properties(
                    ("used", used.Value),
                    ("unused", unused.Value)))),
        };

        _renderer.Render(VirtualNodeFactory.Component(parent), _container);
        renders.ShouldBe(1);

        // The child re-renders because the parent-driven update carries a changed prop the
        // child reads...
        used.Value = "u2";
        _pump.RunUntilIdle();
        renders.ShouldBe(2);

        // ...and re-renders for the unread prop only through the parent-driven path, which
        // compares props: value changed → update. (Fine-grained skip of unread props is the
        // dynamicProps-optimized path.)
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>u2</span></div></root>");
    }

    [Fact]
    public void OptimizedPropsUpdate_HonorsDynamicPropsAndSkipsUnlistedChanges()
    {
        var renders = 0;
        var child = new TestComponent
        {
            Properties = [new ComponentPropertyDefinition("tracked"), new ComponentPropertyDefinition("stable")],
            SetupFunction = (properties, _) => () =>
            {
                renders++;
                return VirtualNodeFactory.Element("span", (string?)properties["tracked"] ?? string.Empty);
            },
        };
        VirtualNode Compiled(string tracked, string stable) => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Component(
                child,
                VirtualNodeFactory.Properties(("tracked", tracked), ("stable", stable)),
                Shared.PatchFlags.Props,
                ["tracked"]));

        _renderer.Render(Compiled("a", "constant"), _container);
        renders.ShouldBe(1);

        _renderer.Render(Compiled("a", "constant"), _container);
        renders.ShouldBe(1); // no listed prop changed: shouldUpdateComponent skips

        // A change to an UNLISTED prop is invisible to the optimized comparison — the
        // compiled contract (upstream parity).
        _renderer.Render(Compiled("a", "drifted"), _container);
        renders.ShouldBe(1);

        _renderer.Render(Compiled("b", "drifted"), _container);
        renders.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div><span>b</span></div></root>");
    }
}
