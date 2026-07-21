using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Render helpers that wrap a single render delegate as a root component.</summary>
internal static class Ssr
{
    /// <summary>Renders <paramref name="render"/> (as a root component) to an HTML string.</summary>
    public static Task<string> RenderAsync(ComponentSetup render, SsrContext? context = null)
        => ServerRenderer.RenderToStringAsync(new InlineComponent((_, _) => render), null, context);

    /// <summary>Renders an <see cref="InlineComponent"/> to an HTML string.</summary>
    public static Task<string> RenderAsync(InlineComponent component, VirtualNodeProperties? properties = null, SsrContext? context = null)
        => ServerRenderer.RenderToStringAsync(component, properties, context);
}

/// <summary>
/// A component defined inline by a setup delegate — the test stand-in for a source-generated
/// component. Setup runs once and returns the render function, exactly like a real component; tests
/// build their vnode trees with <see cref="VirtualNodeFactory"/> inside it.
/// </summary>
internal sealed class InlineComponent : IComponent
{
    private readonly Func<ComponentProperties, ComponentSetupContext, ComponentSetup> _setup;

    public InlineComponent(
        Func<ComponentProperties, ComponentSetupContext, ComponentSetup> setup,
        string? name = null,
        IReadOnlyList<ComponentPropertyDefinition>? properties = null,
        bool inheritAttributes = true)
    {
        _setup = setup;
        Name = name;
        Properties = properties;
        InheritAttributes = inheritAttributes;
    }

    public string? Name { get; }

    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; }

    public bool InheritAttributes { get; }

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
        => _setup(properties, context);
}
