using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Router.Tests;

// Shared fixtures for RouterView/RouterLink against the unified component tree. Router resolution is
// exclusively through the explicit IServiceProvider on IComponentContext; there is no hierarchical
// component-dependency test path.
internal static class RouterComponentsTestSupport
{
    public static ComponentWrapper MountView(
        Router router,
        params TrackingComponent[] components)
    {
        return ViuTest.Mount(
            new RouterView(),
            OptionsFor(router, components));
    }

    public static ComponentWrapper MountLink(
        Router router,
        IComponentArguments arguments,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null)
    {
        ComponentMountOptions options = OptionsFor(router);
        options.Arguments = arguments;
        options.Slots = slots;
        return ViuTest.Mount(new RouterLink(), options);
    }

    public static ComponentMountOptions OptionsFor(
        Router router,
        params TrackingComponent[] components)
    {
        return new ComponentMountOptions
        {
            Components = new RouterTestComponentFactory(components),
            Services = new RouterServiceProvider(router),
        };
    }

    public static TrackingComponent LabelView(string label)
    {
        return new TrackingComponent(
            label,
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", label)),
                [ComponentTree.Text(label)]));
    }

    public static TrackingComponent PropView(string parameterName)
    {
        return new TrackingComponent(
            "parameter-" + parameterName,
            context => ComponentTree.Element(
                "span",
                Attributes(("class", "value")),
                [ComponentTree.Text(
                    context.Arguments.Get<string>(parameterName)
                    ?? string.Empty)]),
            [new ComponentParameter(parameterName)]);
    }

    public static TrackingComponent LayoutView(int outletDepth = 1)
    {
        return new TrackingComponent(
            "layout",
            _ => ComponentTree.Element(
                "div",
                Attributes(("class", "layout")),
                [
                    ComponentTree.Template<RouterView>(
                        Arguments(("depth", outletDepth))),
                ]));
    }

    public static IReadOnlyDictionary<string, ComponentSlot> TextSlot(string text)
    {
        return new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
        {
            ["default"] = _ => ComponentTree.Text(text),
        };
    }

    public static ComponentArguments Arguments(
        params (string Name, object? Value)[] entries)
    {
        List<KeyValuePair<string, object?>> values = new(entries.Length);
        foreach ((string name, object? value) in entries)
        {
            values.Add(new KeyValuePair<string, object?>(name, value));
        }

        return new ComponentArguments(values);
    }

    public static ComponentAttributes Attributes(
        params (string Name, object? Value)[] entries)
    {
        List<IComponentAttribute> attributes = new(entries.Length);
        foreach ((string name, object? value) in entries)
        {
            attributes.Add(new ComponentAttribute(name, value));
        }

        return new ComponentAttributes(attributes);
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

    private sealed class RouterTestComponentFactory : IComponentFactory
    {
        private readonly Dictionary<string, TrackingComponent> _components =
            new(StringComparer.Ordinal);

        internal RouterTestComponentFactory(
            IReadOnlyList<TrackingComponent> components)
        {
            for (int index = 0; index < components.Count; index++)
            {
                TrackingComponent component = components[index];
                _components.Add(component.RegistrationName, component);
            }
        }

        public IComponentTemplate Create(Type componentType)
        {
            if (componentType == typeof(RouterView))
            {
                return new RouterView();
            }

            if (componentType == typeof(RouterLink))
            {
                return new RouterLink();
            }

            throw new InvalidOperationException(
                $"Component type \"{componentType}\" is not registered for the router test.");
        }

        public IComponentTemplate Create(string name)
        {
            return _components.TryGetValue(name, out TrackingComponent? component)
                ? component
                : throw new InvalidOperationException(
                    $"Component name \"{name}\" is not registered for the router test.");
        }
    }
}

internal sealed class TrackingComponent : IComponentTemplate
{
    private static int _nextIdentifier;
    private readonly Func<IComponentContext, IComponent?> _render;
    private readonly Action<IComponentContext>? _setup;

    public TrackingComponent(
        string name,
        Func<IComponentContext, IComponent?> render,
        IReadOnlyList<IComponentParameter>? parameters = null,
        Action<IComponentContext>? setup = null)
    {
        Name = name;
        _render = render;
        _setup = setup;
        Parameters = parameters;
        RegistrationName =
            name + "-" + Interlocked.Increment(ref _nextIdentifier).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        Request = ComponentTree.Template(RegistrationName);
    }

    public string? Name { get; }

    public IReadOnlyList<IComponentParameter>? Parameters { get; }

    public string RegistrationName { get; }

    public ITemplateComponent Request { get; }

    public int SetupCount { get; private set; }

    public int RenderCount { get; private set; }

    public IComponentContext? Context { get; private set; }

    public bool IsUnmounted { get; private set; }

    public ComponentRenderer Setup(IComponentContext context)
    {
        SetupCount++;
        Context = context;
        context.Lifecycle.OnUnmounted(() => IsUnmounted = true);
        _setup?.Invoke(context);
        return () =>
        {
            RenderCount++;
            return _render(context);
        };
    }
}
