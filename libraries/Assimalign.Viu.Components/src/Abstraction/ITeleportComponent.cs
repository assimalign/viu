using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes content whose logical tree position differs from its rendered container.
/// </summary>
/// <remarks>
/// The value stays at its logical position in the component tree while its children are rendered
/// into <see cref="Target"/>. The renderer emits origin anchors where the value sits logically and
/// manages the target range separately, so patching, moves, and unmounting still reach the children
/// through the logical tree; toggling <see cref="IsDisabled"/> moves the content between the two
/// containers. Specified by <c>[BLT-1]</c> through <c>[BLT-4]</c>.
/// </remarks>
public interface ITeleportComponent : IComponent
{
    /// <summary>Gets the target selector or platform container handle.</summary>
    object Target { get; }

    /// <summary>Gets whether the children render at their logical position instead of the target.</summary>
    bool IsDisabled { get; }

    /// <summary>
    /// Gets whether target-side setup waits for the current render's post-flush phase. An enabled
    /// Teleport mounts its children after resolving that target; a disabled Teleport still mounts
    /// its children at the logical position immediately.
    /// </summary>
    bool IsDeferred { get; }

    /// <summary>Gets the teleported children.</summary>
    IReadOnlyList<IComponent> Children { get; }
}
