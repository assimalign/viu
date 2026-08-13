using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class MountedTree<TNode>
    where TNode : notnull
{
    internal IApplicationContext? Application;

    internal MountedNode<TNode>? Root;

    internal List<MountedTransition<TNode>> PendingTransitionRemovals { get; } = [];
}
