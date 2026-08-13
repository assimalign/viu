using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Supplies the named authored state that diagnostic tooling may inspect without reflection.
/// </summary>
/// <remarks>
/// Implementations return a current snapshot on each call. Values may include
/// <see cref="Assimalign.Viu.Reactivity.IReactiveReference"/> instances; consumers read those
/// values through their public untyped contract. The runtime never calls this interface on a
/// render hot path. Specified by <c>[DVT-5]</c>.
/// </remarks>
public interface IComponentInspectionStateProvider
{
    /// <summary>
    /// Gets the current named state values. The returned dictionary is borrowed only for the
    /// duration of the inspection request and must not be mutated by the caller.
    /// </summary>
    /// <returns>The current state keyed by its authored inspection name.</returns>
    IReadOnlyDictionary<string, object?> GetInspectionState();
}
