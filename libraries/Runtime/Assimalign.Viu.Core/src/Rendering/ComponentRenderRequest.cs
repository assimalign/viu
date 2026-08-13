using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Describes one host-owned component activation and render-once operation.
/// </summary>
/// <remarks>Specified by <c>[SSR-10]</c>.</remarks>
public sealed class ComponentRenderRequest
{
    /// <summary>Initializes a render request.</summary>
    /// <param name="component">The immutable component invocation node.</param>
    /// <param name="parent">The optional parent operation scope for nested rendering.</param>
    public ComponentRenderRequest(ComponentNode component, IComponentRenderScope? parent = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        Component = component;
        Parent = parent;
    }

    /// <summary>Gets the immutable component invocation.</summary>
    public ComponentNode Component { get; }

    /// <summary>Gets the optional active parent operation scope.</summary>
    public IComponentRenderScope? Parent { get; }
}
