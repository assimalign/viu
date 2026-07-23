using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable component event declaration.</summary>
public sealed class ComponentEvent : IComponentEvent
{
    /// <summary>Creates an event declaration.</summary>
    /// <param name="name">The event name.</param>
    public ComponentEvent(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <inheritdoc/>
    public string Name { get; }
}

