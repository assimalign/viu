using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Router.Tests;

// Shared fixtures for the RouterView/RouterLink suites ([V01.01.08.03], issue #72): a tracking
// component that pins setup/render run counts (the reactivity contract from testing.md), the small
// view/layout factories the route tables render, and helpers that mount a built-in component against
// the in-memory Testing renderer with the router provided app-wide.
internal static class RouterComponentsTestSupport
{
    // Mounts a RouterView outlet as the tree root with the router provided app-wide.
    public static ComponentWrapper MountView(Router router)
        => ViuTest.Mount(new RouterView(), OptionsFor(router));

    // Mounts a RouterLink as the tree root with the given props/slot and the router provided app-wide.
    public static ComponentWrapper MountLink(Router router, VirtualNodeProperties properties, ComponentSlots? slots = null)
    {
        var options = OptionsFor(router);
        options.Properties = properties;
        options.Slots = slots;
        return ViuTest.Mount(new RouterLink(), options);
    }

    public static ComponentMountOptions OptionsFor(Router router)
    {
        var options = new ComponentMountOptions();
        options.Provide(RouterInjectionKeys.Router, router);
        return options;
    }

    // A view rendering <div class="label">label</div>.
    public static TrackingComponent LabelView(string label)
        => new(
            label,
            _ => VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("class", label)), label));

    // A view rendering <span class="value">{prop}</span>, declaring the named prop.
    public static TrackingComponent PropView(string propertyName)
        => new(
            "prop-" + propertyName,
            properties => VirtualNodeFactory.Element(
                "span",
                VirtualNodeFactory.Properties(("class", "value")),
                properties.Get<string>(propertyName) ?? string.Empty),
            [new ComponentPropertyDefinition(propertyName)]);

    // A layout rendering <div class="layout"><outlet/></div>; the outlet is a stable nested RouterView.
    public static TrackingComponent LayoutView(RouterView outlet)
        => new(
            "layout",
            _ => VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "layout")),
                VirtualNodeFactory.Component(outlet)));

    // A single default slot rendering plain text (RouterLink label content).
    public static ComponentSlots TextSlot(string text)
    {
        var slots = new ComponentSlots();
        slots["default"] = _ => [VirtualNodeFactory.Text(text)];
        return slots;
    }
}

// A component definition that records how many times it was set up and rendered and captures its
// instance, so the suites can assert re-render run counts and remount-vs-patch behavior.
internal sealed class TrackingComponent : IComponent
{
    private readonly Func<ComponentProperties, VirtualNode?> _render;

    public TrackingComponent(
        string name,
        Func<ComponentProperties, VirtualNode?> render,
        IReadOnlyList<ComponentPropertyDefinition>? properties = null)
    {
        Name = name;
        _render = render;
        Properties = properties;
    }

    public string? Name { get; }

    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; }

    public int SetupCount { get; private set; }

    public int RenderCount { get; private set; }

    public ComponentInstance? Instance { get; private set; }

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        SetupCount++;
        Instance = ComponentInstance.Current;
        return () =>
        {
            RenderCount++;
            return _render(properties);
        };
    }
}
