using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The default registration-backed component factory. Unsealed so a composition root can layer
/// an explicit resolution policy; every override must remain reflection-free.
/// </summary>
/// <remarks>
/// Registered-name lookup tries the exact name, its camel-case form, and then the Pascal-case
/// form of that value. Lookup is ordinal and registrations that are equivalent only after
/// normalization remain distinct, so exact matches retain precedence. The factory is not
/// thread-safe. Specified by <c>[CMP-4]</c> and <c>[CMP-6]</c>.
/// </remarks>
public class ComponentFactory : IComponentFactory
{
    private readonly Dictionary<ComponentReference, ComponentRegistration> _registrations = [];

    /// <summary>Registers a component, throwing on a duplicate reference.</summary>
    /// <param name="registration">The explicit registration.</param>
    public void Register(ComponentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _registrations.Add(registration.Reference, registration);
    }

    /// <inheritdoc/>
    public virtual ComponentRegistration Resolve(ComponentReference reference)
    {
        return TryResolve(reference, out ComponentRegistration? registration)
            ? registration!
            : throw new InvalidOperationException(
                "The component reference is not registered; runtime constructor discovery is not supported.");
    }

    /// <inheritdoc/>
    public virtual bool TryResolve(ComponentReference reference, out ComponentRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (_registrations.TryGetValue(reference, out registration))
        {
            return true;
        }

        if (reference.Kind != ComponentReferenceKind.RegisteredName)
        {
            return false;
        }

        string name = reference.RegisteredName!;
        string camelizedName = NameNormalization.Camelize(name);
        if (camelizedName.Length > 0
            && !string.Equals(camelizedName, name, StringComparison.Ordinal)
            && _registrations.TryGetValue(
                ComponentReference.ForName(camelizedName),
                out registration))
        {
            return true;
        }

        string pascalizedName = NameNormalization.Pascalize(camelizedName);
        return pascalizedName.Length > 0
            && !string.Equals(pascalizedName, camelizedName, StringComparison.Ordinal)
            && _registrations.TryGetValue(
                ComponentReference.ForName(pascalizedName),
                out registration);
    }
}
