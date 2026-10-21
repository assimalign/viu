using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a non-activating invocation of registered authored component behavior.
/// </summary>
/// <remarks>Specified by <c>[CMP-7]</c>.</remarks>
public sealed class ComponentNode : VirtualNode
{
    /// <summary>
    /// Describes a typed component without activation or reflection. The definition supplies its
    /// exact registration reference. Specified by <c>[CMP-35]</c>.
    /// </summary>
    /// <typeparam name="TComponent">The generated or code-first component definition.</typeparam>
    /// <param name="invocation">Optional parent-created inputs.</param>
    /// <param name="key">Optional sibling identity.</param>
    /// <returns>An immutable component invocation.</returns>
    public static ComponentNode For<TComponent>(ComponentInvocation? invocation = null, object? key = null)
        where TComponent : IComponentDefinition => new(TComponent.Registration.Reference, invocation, key);

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
