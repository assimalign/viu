using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Identifies the registration produced by an asynchronous-component loader.</summary>
/// <remarks>
/// The target carries only a <see cref="ComponentReference"/> and cannot bypass the application
/// component factory. Specified by <c>[BLT-14]</c>.
/// </remarks>
public readonly struct AsynchronousComponentTarget
{
    private readonly ComponentReference? _reference;

    /// <summary>Creates a target for a registered component type.</summary>
    /// <param name="componentType">The registered <see cref="IComponent"/> type.</param>
    public AsynchronousComponentTarget(Type componentType)
    {
        _reference = ComponentReference.ForType(componentType);
    }

    /// <summary>Creates a target for an explicitly registered component name.</summary>
    /// <param name="componentName">The non-empty registration name.</param>
    public AsynchronousComponentTarget(string componentName)
    {
        _reference = ComponentReference.ForName(componentName);
    }

    /// <summary>Creates a target from an already validated component reference.</summary>
    /// <param name="reference">The explicit registration identity.</param>
    public AsynchronousComponentTarget(ComponentReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _reference = reference;
    }

    /// <summary>Gets the explicit registration identity.</summary>
    /// <exception cref="InvalidOperationException">The value is the default uninitialized target.</exception>
    public ComponentReference Reference => _reference
        ?? throw new InvalidOperationException(
            "An asynchronous component loader returned an uninitialized target.");

    /// <summary>Creates a target for a registered authored component type.</summary>
    /// <typeparam name="TComponent">The registered component type.</typeparam>
    /// <returns>The type-identified target.</returns>
    public static AsynchronousComponentTarget From<TComponent>()
        where TComponent : class, IComponent
    {
        return new AsynchronousComponentTarget(typeof(TComponent));
    }

    internal ComponentNode CreateComponent(
        ComponentInvocation invocation,
        MountReference? mountReference)
    {
        return new ComponentNode(
            Reference,
            invocation,
            mountReference: mountReference);
    }

    internal void Validate()
    {
        _ = Reference;
    }
}
