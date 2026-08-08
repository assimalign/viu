using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedTree<TNode>
    where TNode : notnull
{
    internal IApplicationContext? Application;

    internal MountedNode<TNode>? Root;

    internal Dictionary<VirtualNode, MountedNode<TNode>> Nodes { get; } =
        new(ReferenceEqualityComparer.Instance);
}
