using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes content whose logical tree position differs from its rendered container.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's Teleport contract:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/Teleport.ts.
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
