using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedLeaf<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedLeaf(
        VirtualNode value,
        TNode hostNode,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        HostNode = hostNode;
    }

    internal TNode HostNode;

    internal override TNode FirstHostNode => HostNode;

    internal override TNode LastHostNode => HostNode;
}
