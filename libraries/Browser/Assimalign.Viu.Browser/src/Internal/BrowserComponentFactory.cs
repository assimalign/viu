using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Resolves Browser transition components before delegating to the borrowed application factory.
/// </summary>
internal sealed class BrowserComponentFactory : IComponentFactory
{
    private readonly IComponentFactory _applicationComponents;

    internal BrowserComponentFactory(IComponentFactory applicationComponents)
    {
        ArgumentNullException.ThrowIfNull(applicationComponents);
        _applicationComponents = applicationComponents;
    }

    /// <inheritdoc/>
    public ComponentRegistration Resolve(ComponentReference reference)
    {
        return TryResolve(reference, out ComponentRegistration? registration)
            ? registration!
            : throw new InvalidOperationException(
                "The component reference is not registered; runtime constructor discovery is not supported.");
    }

    /// <inheritdoc/>
    public bool TryResolve(
        ComponentReference reference,
        out ComponentRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (Matches(reference, typeof(Transition), "Transition"))
        {
            registration = Transition.Registration;
            return true;
        }

        if (Matches(reference, typeof(TransitionGroup), "TransitionGroup"))
        {
            registration = TransitionGroup.Registration;
            return true;
        }

        return _applicationComponents.TryResolve(reference, out registration);
    }

    private static bool Matches(
        ComponentReference reference,
        Type componentType,
        string registeredName)
    {
        if (reference.Kind == ComponentReferenceKind.Type)
        {
            return reference.ComponentType == componentType;
        }

        return reference.Kind == ComponentReferenceKind.RegisteredName
            && string.Equals(
                NameNormalization.Pascalize(reference.RegisteredName!),
                registeredName,
                StringComparison.Ordinal);
    }
}
