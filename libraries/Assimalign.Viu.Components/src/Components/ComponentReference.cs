using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies a component definition by exactly one supported lookup form.
/// </summary>
/// <remarks>
/// References are descriptions only and never activate an instance. Name identity is ordinal and
/// resolved through explicit registrations. Specified by <c>[CMP-6]</c>, <c>[CMP-7]</c>, and
/// <c>[BLT-15]</c>.
/// </remarks>
public sealed record ComponentReference
{
    private ComponentReference(
        ComponentReferenceKind kind,
        Type? componentType,
        string? registeredName)
    {
        Kind = kind;
        ComponentType = componentType;
        RegisteredName = registeredName;
    }

    /// <summary>Gets the lookup form.</summary>
    public ComponentReferenceKind Kind { get; }

    /// <summary>Gets the component type token when <see cref="Kind"/> is type-based.</summary>
    public Type? ComponentType { get; }

    /// <summary>Gets the registered name when <see cref="Kind"/> is name-based.</summary>
    public string? RegisteredName { get; }

    /// <summary>Creates a type-based reference without using reflective activation.</summary>
    /// <param name="componentType">The compile-time-known component type.</param>
    /// <returns>The validated reference.</returns>
    public static ComponentReference ForType(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException(
                "A type-based component reference must identify an IComponent implementation.",
                nameof(componentType));
        }

        return new ComponentReference(ComponentReferenceKind.Type, componentType, null);
    }

    /// <summary>Creates a registered-name reference.</summary>
    /// <param name="registeredName">The non-empty application registration name.</param>
    /// <returns>The validated reference.</returns>
    public static ComponentReference ForName(string registeredName)
    {
        ArgumentException.ThrowIfNullOrEmpty(registeredName);
        return new ComponentReference(ComponentReferenceKind.RegisteredName, null, registeredName);
    }
}
