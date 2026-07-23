using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Registers an AOT-safe component activator under a type and optional name.
/// </summary>
public readonly struct ComponentRegistration
{
    /// <summary>Creates a component registration.</summary>
    /// <param name="componentType">The explicit lookup type.</param>
    /// <param name="activator">The delegate that creates a fresh template.</param>
    /// <param name="name">The optional string lookup name.</param>
    public ComponentRegistration(Type componentType, ComponentActivator activator, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(activator);
        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
        }

        ComponentType = componentType;
        Activator = activator;
        Name = name;
    }

    /// <summary>Gets the explicit component lookup type.</summary>
    public Type ComponentType { get; }

    /// <summary>Gets the optional string lookup name.</summary>
    public string? Name { get; }

    /// <summary>Gets the AOT-safe activation delegate.</summary>
    public ComponentActivator Activator { get; }
}

