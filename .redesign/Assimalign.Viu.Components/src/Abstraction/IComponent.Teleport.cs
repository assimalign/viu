using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes content whose logical tree position differs from its rendered container.
/// </summary>
public interface ITeleportComponent : IComponent
{
    /// <summary>Gets the target selector or platform container handle.</summary>
    object Target { get; }

    /// <summary>Gets whether the children render at their logical position instead of the target.</summary>
    bool IsDisabled { get; }

    /// <summary>Gets the teleported children.</summary>
    IReadOnlyList<IComponent> Children { get; }
}

