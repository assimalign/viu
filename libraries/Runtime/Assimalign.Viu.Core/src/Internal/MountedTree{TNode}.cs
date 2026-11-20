using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class MountedTree<TNode>
    where TNode : notnull
{
    internal IApplicationContext? Application;

    internal MountedNode<TNode>? Root;

    /// <summary>
    /// The number of mounted teleports in this tree that currently own a resolved target range.
    /// Target-order normalization only has work to do when two or more teleports can share one
    /// target, so this count lets every other render skip the full mounted-tree walk.
    /// </summary>
    internal int TargetedTeleportCount;

    internal List<MountedTransition<TNode>> PendingTransitionRemovals { get; } = [];
}
