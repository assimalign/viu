namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// Why a <see cref="VirtualNodeType.Teleport"/>'s nodes are being moved — the C# port of upstream's
/// <c>TeleportMoveTypes</c> (<c>packages/runtime-core/src/components/Teleport.ts</c>). The value picks
/// which anchors move and whether the children travel with them (see the renderer's
/// <c>MoveTeleport</c>). [V01.01.03.17]
/// </summary>
internal enum TeleportMoveType
{
    /// <summary>
    /// The <c>to</c> target changed: the target anchor is inserted into the new container and the
    /// children follow it (upstream: <c>TARGET_CHANGE</c>).
    /// </summary>
    TargetChange,

    /// <summary>
    /// <c>disabled</c> toggled: the children move between the main-tree position and the target
    /// container without unmounting, so subtree state is preserved (upstream: <c>TOGGLE</c>).
    /// </summary>
    Toggle,

    /// <summary>
    /// The whole Teleport is being relocated in the main tree (e.g. a keyed reorder): the main-tree
    /// anchor pair moves, and the children move only when the Teleport is disabled (enabled children
    /// stay in the target) (upstream: <c>REORDER</c>).
    /// </summary>
    Reorder,
}
