using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Router.Tests;

// Shared DOM-free fixtures for the adopted component model. Router resolution is exclusively
// through ComponentContext.Services; no hierarchical convention lookup or context cast exists.
internal static class RouterComponentsTestSupport
{
    public static ComponentWrapper MountView(
        Router router,
        params TrackingComponent[] components)
    {
        return ComponentTest.Mount(RouterView.Registration, OptionsFor(router, components));
    }

    public static ComponentWrapper MountLink(
        Router router,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null)
    {
        ComponentMountOptions options = OptionsFor(router);
        options.Arguments = arguments;
        options.Slots = slots;
        return ComponentTest.Mount(RouterLink.Registration, options);
    }

    public static ComponentMountOptions OptionsFor(
        Router router,
        params TrackingComponent[] components)
    {
        ComponentFactory componentFactory = new();
        componentFactory.Register(RouterView.Registration);
        componentFactory.Register(RouterLink.Registration);
        for (int index = 0; index < components.Length; index++)
        {
            componentFactory.Register(components[index].Registration);
        }

        return new ComponentMountOptions
        {
            Components = componentFactory,
            Services = new RouterServiceProvider(router),
        };
    }

    public static TrackingComponent LabelView(string label)
    {
        return new TrackingComponent(
            label,
            _ => Element(
                "div",
                Attributes(("class", label)),
                [Text(label)]));
    }

    public static TrackingComponent PropView(string parameterName)
    {
        return new TrackingComponent(
            "parameter-" + parameterName,
            context => Element(
                "span",
                Attributes(("class", "value")),
                [
                    Text(
                        context.Bindings.Parameters.TryGetValue(
                            parameterName,
                            out object? value)
                                ? value as string ?? string.Empty
                                : string.Empty),
                ]),
            [new ComponentParameter(parameterName)]);
    }

    public static TrackingComponent LayoutView(int outletDepth = 1)
    {
        return new TrackingComponent(
            "layout",
            _ => Element(
                "div",
                Attributes(("class", "layout")),
                [Component<RouterView>(Arguments(("depth", outletDepth)))]));
    }

    public static IReadOnlyDictionary<string, ComponentSlot> TextSlot(string text)
    {
        return new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
        {
            ["default"] = _ => Text(text),
        };
    }

    public static IReadOnlyDictionary<string, object?> Arguments(
        params (string Name, object? Value)[] entries)
    {
        Dictionary<string, object?> values = new(entries.Length, StringComparer.Ordinal);
        foreach ((string name, object? value) in entries)
        {
            values.Add(name, value);
        }

        return values;
    }

    public static IReadOnlyList<ElementBinding> Attributes(
        params (string Name, object? Value)[] entries)
    {
        List<ElementBinding> attributes = new(entries.Length);
        foreach ((string name, object? value) in entries)
        {
            attributes.Add(ElementBinding.Attribute(new QualifiedName(name), value));
        }

        return attributes;
    }

    public static ElementNode Element(
        string name,
        IEnumerable<ElementBinding>? bindings = null,
        IEnumerable<VirtualNode>? children = null)
    {
        return new ElementNode(new QualifiedName(name), bindings, children);
    }

    public static TextNode Text(string value)
    {
        return new TextNode(value);
    }

    public static ComponentNode Component<TComponent>(
        IReadOnlyDictionary<string, object?>? arguments = null)
        where TComponent : class, IComponent
    {
        return new ComponentNode(
            ComponentReference.ForType(typeof(TComponent)),
            new ComponentInvocation(arguments));
    }

    private sealed class RouterServiceProvider : IServiceProvider
    {
        private readonly Router _router;

        internal RouterServiceProvider(Router router)
        {
            _router = router;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(Router) ? _router : null;
        }
    }
}

internal sealed class TrackingComponent : IComponent
{
    private static int _nextIdentifier;
    private readonly Func<ComponentContext, VirtualNode?> _render;
    private readonly Action<ComponentContext>? _setup;

    public TrackingComponent(
        string name,
        Func<ComponentContext, VirtualNode?> render,
        IReadOnlyList<ComponentParameter>? parameters = null,
        Action<ComponentContext>? setup = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(render);
        _render = render;
        _setup = setup;
        string registrationName =
            name + "-" + Interlocked.Increment(ref _nextIdentifier).ToString(
                CultureInfo.InvariantCulture);
        Registration = new ComponentRegistration(
            ComponentReference.ForName(registrationName),
            new ComponentContract(
                renderCacheSize: 0,
                displayName: name,
                parameters: parameters),
            _ => this);
        Request = new ComponentNode(Registration.Reference);
    }

    public ComponentRegistration Registration { get; }

    public ComponentNode Request { get; }

    public int SetupCount { get; private set; }

    public int RenderCount { get; private set; }

    public ComponentContext? Context { get; private set; }

    public bool IsUnmounted { get; private set; }

    public ComponentRenderer Setup(ComponentContext context)
    {
        SetupCount++;
        Context = context;
        context.Lifecycle.OnUnmounted(() => IsUnmounted = true);
        _setup?.Invoke(context);
        return _ =>
        {
            RenderCount++;
            return _render(context);
        };
    }
}
