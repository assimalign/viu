using System;

namespace Assimalign.Viu;

/// <summary>Selects a component-factory registration by name in a dynamic expression.</summary>
/// <remarks>
/// Plain strings remain element names; this explicit marker removes ambiguity without probing the
/// component factory. Specified by <c>[BLT-15]</c>.
/// </remarks>
public readonly struct DynamicComponentName
{
    /// <summary>Initializes a named dynamic-component selector.</summary>
    /// <param name="name">The non-empty registration name.</param>
    public DynamicComponentName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Gets the explicit registration name.</summary>
    public string Name { get; }
}
