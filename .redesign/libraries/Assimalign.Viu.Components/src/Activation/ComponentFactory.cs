using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The default registration-backed component factory. Unsealed as a documented sealing
/// exception so a host can layer resolution policy; every override must remain reflection-free.
/// </summary>
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
        return _registrations.TryGetValue(reference, out registration);
    }
}
