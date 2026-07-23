using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable teleport component.</summary>
public sealed class TeleportComponent : ITeleportComponent
{
    /// <summary>Creates a teleport component.</summary>
    /// <param name="target">The target selector or platform container handle.</param>
    /// <param name="children">The teleported children.</param>
    /// <param name="isDisabled">Whether to render at the logical position.</param>
    /// <param name="key">The optional sibling identity.</param>
    public TeleportComponent(
        object target,
        IReadOnlyList<IComponent>? children = null,
        bool isDisabled = false,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        Children = ComponentChildren.Copy(children);
        IsDisabled = isDisabled;
        Key = key;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Teleport;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public object Target { get; }

    /// <inheritdoc/>
    public bool IsDisabled { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IComponent> Children { get; }
}

