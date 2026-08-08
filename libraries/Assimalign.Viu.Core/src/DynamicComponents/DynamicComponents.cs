using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Creates closed-algebra nodes for explicit dynamic structure.</summary>
/// <remarks>
/// A plain string always creates an element. Registered names require <see cref="Named"/>, so the
/// runtime never probes or guesses against the component factory. Specified by <c>[BLT-15]</c>.
/// </remarks>
public static class DynamicComponents
{
    /// <summary>Creates an explicit registered-name selector.</summary>
    /// <param name="name">The component registration name.</param>
    /// <returns>The unambiguous named selector.</returns>
    public static DynamicComponentName Named(string name) => new(name);

    /// <summary>Normalizes a dynamic selector without activating a component.</summary>
    /// <param name="source">
    /// A qualified or plain element name, type/reference/name component selector, asynchronous
    /// definition, existing node, or null.
    /// </param>
    /// <returns>The unchanged supported selector, or null for an empty or unsupported value.</returns>
    public static object? ResolveDynamicComponent(object? source)
    {
        return source switch
        {
            string { Length: 0 } => null,
            string => source,
            QualifiedName name when !string.IsNullOrEmpty(name.LocalName) => source,
            Type componentType when typeof(IComponent).IsAssignableFrom(componentType) => source,
            ComponentReference => source,
            DynamicComponentName name when !string.IsNullOrEmpty(name.Name) => source,
            AsynchronousComponentDefinition => source,
            VirtualNode => source,
            _ => null,
        };
    }

    /// <summary>Creates the node selected by a dynamic component expression.</summary>
    /// <param name="source">The dynamic selector.</param>
    /// <param name="invocation">Inputs for a selected component.</param>
    /// <param name="bindings">Host bindings for a selected element.</param>
    /// <param name="children">Children for a selected element.</param>
    /// <param name="directives">Directives for a selected element.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="mountReference">The optional mounted-value receiver.</param>
    /// <param name="renderPlan">The compiler patch information.</param>
    /// <returns>A component, element, existing node, or empty comment placeholder.</returns>
    public static VirtualNode DynamicComponent(
        object? source,
        ComponentInvocation? invocation = null,
        IEnumerable<ElementBinding>? bindings = null,
        IEnumerable<VirtualNode>? children = null,
        IEnumerable<DirectiveInvocation>? directives = null,
        object? key = null,
        MountReference? mountReference = null,
        RenderPlan? renderPlan = null)
    {
        object? resolved = ResolveDynamicComponent(source);
        return resolved switch
        {
            AsynchronousComponentDefinition definition => definition.CreateComponent(
                invocation,
                key,
                mountReference,
                renderPlan),
            ComponentReference reference => new ComponentNode(
                reference,
                invocation,
                key,
                mountReference,
                renderPlan),
            Type componentType => new ComponentNode(
                ComponentReference.ForType(componentType),
                invocation,
                key,
                mountReference,
                renderPlan),
            DynamicComponentName name => new ComponentNode(
                ComponentReference.ForName(name.Name),
                invocation,
                key,
                mountReference,
                renderPlan),
            QualifiedName elementName => new ElementNode(
                elementName,
                bindings,
                children,
                directives,
                key,
                mountReference,
                renderPlan),
            string elementName => new ElementNode(
                new QualifiedName(elementName),
                bindings,
                children,
                directives,
                key,
                mountReference,
                renderPlan),
            VirtualNode node => node,
            _ => new CommentNode(string.Empty),
        };
    }
}
