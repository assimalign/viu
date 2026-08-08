using System;

namespace Assimalign.Viu.Components;

/// <summary>Declares one generated component input on the property that receives its value.</summary>
/// <remarks>
/// The source generator reads the declaration and writes the corresponding
/// <see cref="ComponentParameter"/> into the component's static contract. Runtime code performs no
/// attribute discovery. The authored initializer is the per-instance default restored whenever an
/// invocation omits the parameter. Specified by <c>[CMP-26]</c> through <c>[CMP-29]</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ParameterAttribute : Attribute
{
    /// <summary>Declares a parameter whose template name is derived from the property name.</summary>
    public ParameterAttribute()
    {
    }

    /// <summary>Declares a parameter with an explicit template-facing name.</summary>
    /// <param name="name">The non-empty canonical argument name.</param>
    public ParameterAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets or sets the explicit name, or null to derive it from the property name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets whether an invocation must supply the parameter.</summary>
    public bool IsRequired { get; set; }
}
