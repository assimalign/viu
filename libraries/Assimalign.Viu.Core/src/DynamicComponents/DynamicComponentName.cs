using System;

namespace Assimalign.Viu;

/// <summary>
/// Selects a component-factory registration by name in a dynamic component expression.
/// </summary>
/// <remarks>
/// Plain strings deliberately remain element tags. This explicit marker removes the ambiguity
/// without adding a probing or activation method to <c>IComponentFactory</c>.
/// </remarks>
public readonly struct DynamicComponentName
{
    /// <summary>Creates a named dynamic-component selector.</summary>
    /// <param name="name">The component-factory registration name.</param>
    public DynamicComponentName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the component-factory registration name.</summary>
    public string Name { get; }
}
