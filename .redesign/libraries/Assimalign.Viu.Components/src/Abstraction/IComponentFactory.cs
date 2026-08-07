namespace Assimalign.Viu.Components;

/// <summary>
/// Resolves component references to their explicit registrations. Registration is the only
/// activation path — there is no runtime constructor discovery.
/// </summary>
public interface IComponentFactory
{
    /// <summary>Resolves a reference to its registration, throwing when unregistered.</summary>
    /// <param name="reference">The type or name reference carried by a component node.</param>
    /// <returns>The matching registration.</returns>
    ComponentRegistration Resolve(ComponentReference reference);

    /// <summary>Attempts to resolve a reference to its registration.</summary>
    /// <param name="reference">The type or name reference carried by a component node.</param>
    /// <param name="registration">The matching registration when found.</param>
    /// <returns>Whether a registration was found.</returns>
    bool TryResolve(ComponentReference reference, out ComponentRegistration? registration);
}
