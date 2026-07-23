using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Associates one direct keyed render child with its first mounted host element.
/// </summary>
/// <typeparam name="TNode">The host element type supplied by the renderer.</typeparam>
public sealed class KeyedComponentHostElement<TNode>
    where TNode : notnull
{
    /// <summary>Creates a keyed child-to-host-element snapshot.</summary>
    /// <param name="component">The immutable direct child description.</param>
    /// <param name="key">The non-null child identity.</param>
    /// <param name="element">The child's first mounted host element.</param>
    public KeyedComponentHostElement(
        IComponent component,
        object key,
        TNode element)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(element);
        Component = component;
        Key = key;
        Element = element;
    }

    /// <summary>Gets the immutable direct child description.</summary>
    public IComponent Component { get; }

    /// <summary>Gets the non-null child identity.</summary>
    public object Key { get; }

    /// <summary>Gets the child's first mounted host element.</summary>
    public TNode Element { get; }
}
