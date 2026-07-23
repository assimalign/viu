using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable component parameter declaration.</summary>
public sealed class ComponentParameter : IComponentParameter
{
    /// <summary>Creates a component parameter declaration.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="isRequired">Whether the caller must supply the parameter.</param>
    /// <param name="defaultFactory">The optional per-mount default factory.</param>
    /// <param name="validator">
    /// The optional validator that returns whether a resolved value is valid.
    /// </param>
    public ComponentParameter(
        string name,
        bool isRequired = false,
        Func<object?>? defaultFactory = null,
        Func<object?, bool>? validator = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        IsRequired = isRequired;
        DefaultFactory = defaultFactory;
        Validator = validator;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsRequired { get; }

    /// <inheritdoc/>
    public Func<object?>? DefaultFactory { get; }

    /// <inheritdoc/>
    public Func<object?, bool>? Validator { get; }
}
