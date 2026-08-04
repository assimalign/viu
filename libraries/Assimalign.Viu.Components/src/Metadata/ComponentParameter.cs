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
    /// <param name="parameterType">
    /// The optional declared value type. It is descriptive only — Core never converts a supplied
    /// argument to it — and exists so the declaration carries the same information in metadata that
    /// it carries in source ([SFC-USE-2]).
    /// </param>
    public ComponentParameter(
        string name,
        bool isRequired = false,
        Func<object?>? defaultFactory = null,
        Func<object?, bool>? validator = null,
        Type? parameterType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        IsRequired = isRequired;
        DefaultFactory = defaultFactory;
        Validator = validator;
        ParameterType = parameterType;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsRequired { get; }

    /// <inheritdoc/>
    public Func<object?>? DefaultFactory { get; }

    /// <inheritdoc/>
    public Func<object?, bool>? Validator { get; }

    /// <inheritdoc/>
    public Type? ParameterType { get; }
}
