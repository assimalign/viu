using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a subtree rendered into a target resolved by the active host.
/// </summary>
/// <remarks>Specified by <c>[BLT-1]</c> through <c>[BLT-4]</c>.</remarks>
public sealed class TeleportNode : CompositeVirtualNode
{
    /// <summary>Initializes an immutable teleport node.</summary>
    /// <param name="targetIdentifier">The non-empty host-resolved target identifier.</param>
    /// <param name="children">The teleported children.</param>
    /// <param name="isDisabled">Whether the children render in place instead of at the target.</param>
    /// <param name="isDeferred">Whether target resolution waits for the surrounding mount.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="renderPlan">Optional compiler patch information for teleported content.</param>
    public TeleportNode(
        string targetIdentifier,
        IEnumerable<VirtualNode>? children = null,
        bool isDisabled = false,
        bool isDeferred = false,
        object? key = null,
        RenderPlan? renderPlan = null)
        : base(VirtualNodeKind.Teleport, children, key, null, renderPlan)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetIdentifier);
        TargetIdentifier = targetIdentifier;
        IsDisabled = isDisabled;
        IsDeferred = isDeferred;
    }

    /// <summary>Gets the identifier interpreted by the active host target resolver.</summary>
    public string TargetIdentifier { get; }

    /// <summary>Gets whether the children render in place instead of at the target.</summary>
    public bool IsDisabled { get; }

    /// <summary>Gets whether target resolution waits for the surrounding mount.</summary>
    public bool IsDeferred { get; }
}
