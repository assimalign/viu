using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable component attribute.</summary>
public sealed class ComponentAttribute : IComponentAttribute
{
    /// <summary>Creates a component attribute.</summary>
    /// <param name="name">The binding name.</param>
    /// <param name="value">The binding value.</param>
    public ComponentAttribute(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Value = value;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public object? Value { get; }
}

