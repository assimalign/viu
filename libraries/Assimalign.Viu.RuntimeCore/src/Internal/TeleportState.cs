namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The renderer-owned target-side state of a <see cref="VirtualNodeType.Teleport"/> vnode — the C#
/// stand-in for the <c>target</c>/<c>targetStart</c>/<c>targetAnchor</c> back-pointers upstream hangs
/// directly on the Teleport vnode (<c>packages/runtime-core/src/components/Teleport.ts</c>). Kept as
/// a single nullable reference on <see cref="VirtualNode.TeleportState"/> so a plain vnode pays only
/// one null field on the hot path, and allocated only when a Teleport actually mounts.
/// <para>
/// The main-tree anchor pair framing the Teleport's original position reuses the shared
/// <see cref="VirtualNode.El"/> (start) and <see cref="VirtualNode.Anchor"/> (end) fields; this holder
/// carries only the target-container container/anchors and the deferred-mount job. The node handles
/// are stored boxed as <see cref="object"/> because <see cref="VirtualNode"/> is not generic over the
/// platform node type (matching how <see cref="VirtualNode.El"/> boxes its <c>TNode</c>). Not
/// thread-safe (single-threaded JS event-loop model). [V01.01.03.17]
/// </para>
/// </summary>
internal sealed class TeleportState
{
    /// <summary>
    /// The resolved target container the children are teleported into (upstream: <c>vnode.target</c>),
    /// or null when the <c>to</c> target could not be resolved. A boxed platform node handle.
    /// </summary>
    internal object? Target { get; set; }

    /// <summary>
    /// The start anchor framing the teleported content inside <see cref="Target"/> (upstream:
    /// <c>vnode.targetStart</c>). A boxed platform node handle; null until the target anchors are
    /// prepared.
    /// </summary>
    internal object? TargetStart { get; set; }

    /// <summary>
    /// The end anchor inside <see cref="Target"/> that the teleported children mount before (upstream:
    /// <c>vnode.targetAnchor</c>). A boxed platform node handle; null until the target anchors are
    /// prepared.
    /// </summary>
    internal object? TargetAnchor { get; set; }

    /// <summary>
    /// The post-flush job that resolves the target and mounts the children for a <c>defer</c>-ed
    /// Teleport (upstream: the <c>pendingMounts</c> entry queued by <c>queuePendingMount</c>), or null
    /// when the Teleport mounted synchronously. Superseded on update and disposed on unmount so a stale
    /// deferred mount cannot run after the Teleport has moved or been torn down.
    /// </summary>
    internal SchedulerJob? PendingMount { get; set; }
}
