using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable component parameter declaration.</summary>
public sealed class ComponentParameter : IComponentParameter
{
    /// <summary>Creates a component parameter declaration.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="isRequired">Whether the caller must supply the parameter.</param>
    /// <param name="defaultFactory">The optional per-mount default factory.</param>
    public ComponentParameter(
        string name,
        bool isRequired = false,
        Func<object?>? defaultFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        IsRequired = isRequired;
        DefaultFactory = defaultFactory;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsRequired { get; }

    /// <inheritdoc/>
    public Func<object?>? DefaultFactory { get; }
}
