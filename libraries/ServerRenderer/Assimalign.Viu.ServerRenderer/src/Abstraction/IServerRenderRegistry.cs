using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Resolves compiler-produced server renders from explicit component registrations.
/// </summary>
/// <remarks>
/// A generated assembly catalog populates the registry at the host composition root. Resolution
/// performs no assembly scanning, method inspection, or dynamic activation. Specified by
/// <c>[SSR-TARGET-2]</c> and <c>[SSR-TARGET-3]</c>.
/// </remarks>
public interface IServerRenderRegistry
{
    /// <summary>Attempts to resolve the direct server render for one registered component identity.</summary>
    /// <param name="reference">The exact reference from the resolved component registration.</param>
    /// <param name="registration">The compiled server registration when present.</param>
    /// <returns>Whether the reference has a compiler-produced server render.</returns>
    bool TryResolve(
        ComponentReference reference,
        out ServerRenderRegistration? registration);
}
