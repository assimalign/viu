using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a non-activating invocation of registered authored component behavior.
/// </summary>
public sealed class ComponentNode : VirtualNode
{
    /// <summary>Initializes an immutable component invocation node.</summary>
    /// <param name="component">The definition reference.</param>
    /// <param name="invocation">Raw parent-created inputs.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="mountReference">The optional component-exposure callback.</param>
    /// <param name="renderPlan">Optional compiler patch information.</param>
    public ComponentNode(
        ComponentReference component,
        ComponentInvocation? invocation = null,
        object? key = null,
        MountReference? mountReference = null,
        RenderPlan? renderPlan = null)
        : base(VirtualNodeKind.Component, key, mountReference, renderPlan)
    {
        ArgumentNullException.ThrowIfNull(component);
        Component = component;
        Invocation = invocation ?? ComponentInvocation.Empty;
    }

    /// <summary>Gets the component definition reference.</summary>
    public ComponentReference Component { get; }

    /// <summary>Gets raw parent-created inputs.</summary>
    public ComponentInvocation Invocation { get; }
}
