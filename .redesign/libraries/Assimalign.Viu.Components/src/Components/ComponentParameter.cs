using System;

namespace Assimalign.Viu.Components;

/// <summary>An immutable declared input parameter.</summary>
public sealed class ComponentParameter
{
    /// <summary>Initializes a parameter declaration.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="isRequired">Whether the caller must supply the parameter.</param>
    /// <param name="defaultFactory">The optional per-mount default factory.</param>
    /// <param name="validator">The optional resolved-value validator.</param>
    /// <param name="parameterType">
    /// The optional declared value type — descriptive only; the runtime never converts.
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

    /// <summary>Gets the parameter name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the caller must supply the parameter.</summary>
    public bool IsRequired { get; }

    /// <summary>Gets the optional per-mount default factory.</summary>
    public Func<object?>? DefaultFactory { get; }

    /// <summary>Gets the optional resolved-value validator.</summary>
    public Func<object?, bool>? Validator { get; }

    /// <summary>Gets the optional declared value type, descriptive only.</summary>
    public Type? ParameterType { get; }
}
