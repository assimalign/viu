using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable platform-element component.</summary>
public sealed class ElementComponent : IElementComponent
{
    /// <summary>Creates an element component.</summary>
    /// <param name="tag">The platform tag name.</param>
    /// <param name="attributes">The attributes and event bindings.</param>
    /// <param name="children">The element children.</param>
    /// <param name="key">The optional sibling identity.</param>
    public ElementComponent(
        string tag,
        IComponentAttributeCollection? attributes = null,
        IReadOnlyList<IComponent>? children = null,
        object? key = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        Tag = tag;
        Attributes = attributes ?? new ComponentAttributes();
        Children = ComponentChildren.Copy(children);
        Key = key;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Element;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public string Tag { get; }

    /// <inheritdoc/>
    public IComponentAttributeCollection Attributes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IComponent> Children { get; }
}

