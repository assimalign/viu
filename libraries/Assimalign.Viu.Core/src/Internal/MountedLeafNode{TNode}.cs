using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedLeafNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedLeafNode(
        IComponent component,
        TNode hostNode,
        ComponentContext? owner)
        : base(component, owner)
    {
        HostNode = hostNode;
    }

    internal TNode HostNode;

    internal override TNode FirstHostNode => HostNode;

    internal override TNode LastHostNode => HostNode;
}
