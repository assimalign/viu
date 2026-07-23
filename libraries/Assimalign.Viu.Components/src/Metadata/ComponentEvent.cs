using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable component event declaration.</summary>
public sealed class ComponentEvent : IComponentEvent
{
    /// <summary>Creates an event declaration.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="validator">
    /// The optional validator that returns whether the emitted arguments are valid.
    /// </param>
    public ComponentEvent(
        string name,
        Func<IReadOnlyList<object?>, bool>? validator = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Validator = validator;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Func<IReadOnlyList<object?>, bool>? Validator { get; }
}
