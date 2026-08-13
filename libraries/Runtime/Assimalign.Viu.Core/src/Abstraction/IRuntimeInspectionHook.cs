using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Receives cold-path component diagnostics from the renderer without coupling Core to a
/// diagnostics implementation.
/// </summary>
/// <remarks>
/// Core calls this contract only while <see cref="RuntimeInspection.IsEnabled"/> is true. Hook
/// failures are isolated from rendering. Implementations must enqueue and return immediately;
/// they must never synchronously re-enter a renderer. Specified by <c>[DVT-1]</c> and
/// <c>[DVT-4]</c>.
/// </remarks>
public interface IRuntimeInspectionHook
{
    /// <summary>Observes a component after its mounted subtree has been created.</summary>
    /// <param name="component">The current reflection-free component inspection view.</param>
    void ComponentMounted(in RuntimeInspectionComponent component);

    /// <summary>Observes current component inputs and state after an update.</summary>
    /// <param name="component">The current reflection-free component inspection view.</param>
    void ComponentUpdated(in RuntimeInspectionComponent component);

    /// <summary>Observes the end of a component's mounted lifetime.</summary>
    /// <param name="instance">The authored instance whose mounted lifetime ended.</param>
    void ComponentUnmounted(object instance);

    /// <summary>Observes the structural index of a live component after child reconciliation.</summary>
    /// <param name="instance">The authored instance that remains mounted.</param>
    /// <param name="index">The zero-based index among direct component children of its parent.</param>
    void ComponentReordered(object instance, int index);

    /// <summary>Observes one component event after validation and listener delivery.</summary>
    /// <param name="instance">The authored instance that emitted the event.</param>
    /// <param name="name">The canonical emitted name supplied by the component.</param>
    /// <param name="arguments">The immutable ordered event arguments.</param>
    void ComponentEvent(
        object instance,
        string name,
        IReadOnlyList<object?> arguments);
}
