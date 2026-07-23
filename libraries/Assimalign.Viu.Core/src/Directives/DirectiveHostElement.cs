using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Pairs an immutable element component with the host element mounted for that render value.
/// </summary>
public sealed class DirectiveHostElement
{
    internal DirectiveHostElement(
        IElementComponent component,
        object element)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(element);
        Component = component;
        Element = element;
    }

    /// <summary>Gets the immutable element component.</summary>
    public IElementComponent Component { get; }

    /// <summary>Gets the boxed host element.</summary>
    public object Element { get; }
}
