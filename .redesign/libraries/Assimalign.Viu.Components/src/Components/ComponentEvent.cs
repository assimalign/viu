using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable declared output event.</summary>
public sealed class ComponentEvent
{
    /// <summary>Initializes an event declaration.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="validator">The optional emitted-arguments validator.</param>
    public ComponentEvent(string name, Func<IReadOnlyList<object?>, bool>? validator = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Validator = validator;
    }

    /// <summary>Gets the event name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional emitted-arguments validator.</summary>
    public Func<IReadOnlyList<object?>, bool>? Validator { get; }
}
