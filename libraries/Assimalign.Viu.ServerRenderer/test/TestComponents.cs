using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Focused helpers for building unified component trees in server-renderer tests.</summary>
internal static class Ssr
{
    internal static Task<string> RenderAsync(
        Func<IComponent> render,
        SsrContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(render);
        return ServerRenderer.RenderToStringAsync(render(), context);
    }

    internal static Task<string> RenderAsync(
        InlineComponent component,
        IComponentArguments? arguments = null,
        SsrContext? context = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        ServerApplication application = new(
            component.Request(arguments),
            InlineComponentFactory.Instance,
            services ?? TestServiceProvider.Empty);
        return ServerRenderer.RenderToStringAsync(application, context);
    }

    internal static ServerApplication Application(
        IComponent root,
        IServiceProvider? services = null)
    {
        return new ServerApplication(
            root,
            InlineComponentFactory.Instance,
            services ?? TestServiceProvider.Empty);
    }
}

internal static class TestTree
{
    internal static IComponentAttributeCollection Attributes(
        params (string Name, object? Value)[] values)
    {
        List<IComponentAttribute> attributes = new(values.Length);
        foreach ((string name, object? value) in values)
        {
            attributes.Add(new ComponentAttribute(name, value));
        }

        return new ComponentAttributes(attributes);
    }

    internal static IComponentArguments Arguments(
        params (string Name, object? Value)[] values)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal);
        foreach ((string name, object? value) in values)
        {
            arguments[name] = value;
        }

        return new ComponentArguments(arguments);
    }

    internal static IElementComponent Element(
        string tag,
        IComponentAttributeCollection? attributes = null,
        params IComponent[] children)
    {
        return ComponentTree.Element(tag, attributes, children);
    }

    internal static IElementComponent Element(string tag, string text)
    {
        return ComponentTree.Element(tag, children: [ComponentTree.Text(text)]);
    }
}

/// <summary>
/// A reusable inline template definition whose factory creates a fresh template for every request.
/// </summary>
internal sealed class InlineComponent
{
    private static int _nextIdentifier;
    private readonly Func<IComponentContext, ComponentRenderer> _setup;

    internal InlineComponent(
        Func<IComponentContext, ComponentRenderer> setup,
        string? name = null,
        IReadOnlyList<IComponentParameter>? parameters = null,
        bool inheritAttributes = true,
        string? scopeIdentifier = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _setup = setup;
        Name = name ?? "InlineComponent" + Interlocked.Increment(ref _nextIdentifier);
        Parameters = parameters;
        Flags = inheritAttributes
            ? ComponentFlags.InheritAttributes
            : ComponentFlags.None;
        ScopeIdentifier = scopeIdentifier;
        InlineComponentFactory.Instance.Register(Name, CreateTemplate);
    }

    internal string Name { get; }

    internal IReadOnlyList<IComponentParameter>? Parameters { get; }

    internal ComponentFlags Flags { get; }

    internal string? ScopeIdentifier { get; }

    internal ITemplateComponent Request(
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null)
    {
        return ComponentTree.Template(Name, arguments, slots);
    }

    private IComponentTemplate CreateTemplate() => new Template(this);

    private sealed class Template : IComponentTemplate
    {
        private readonly InlineComponent _definition;

        internal Template(InlineComponent definition)
        {
            _definition = definition;
        }

        public string? Name => _definition.Name;

        public string? ScopeIdentifier => _definition.ScopeIdentifier;

        public ComponentFlags Flags => _definition.Flags;

        public IReadOnlyList<IComponentParameter>? Parameters => _definition.Parameters;

        public ComponentRenderer Setup(IComponentContext context) =>
            _definition._setup(context);
    }
}

internal sealed class InlineComponentFactory : IComponentFactory
{
    private readonly Dictionary<string, Func<IComponentTemplate>> _activators =
        new(StringComparer.Ordinal);

    internal static InlineComponentFactory Instance { get; } = new();

    private InlineComponentFactory()
    {
    }

    internal void Register(string name, Func<IComponentTemplate> activator)
    {
        _activators[name] = activator;
    }

    public IComponentTemplate Create(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        throw new InvalidOperationException(
            $"No inline test component is registered by type \"{componentType}\".");
    }

    public IComponentTemplate Create(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _activators.TryGetValue(name, out Func<IComponentTemplate>? activator)
            ? activator()
            : throw new InvalidOperationException(
                $"No inline test component is registered as \"{name}\".");
    }
}

internal sealed class TestServiceProvider : IServiceProvider
{
    private readonly IReadOnlyDictionary<Type, object> _services;

    internal static TestServiceProvider Empty { get; } =
        new(new Dictionary<Type, object>());

    internal TestServiceProvider(IReadOnlyDictionary<Type, object> services)
    {
        _services = services;
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _services.TryGetValue(serviceType, out object? service)
            ? service
            : null;
    }
}
