using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedStaticNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedStaticNode(
        IStaticComponent component,
        TNode firstHostNode,
        TNode lastHostNode,
        ComponentContext? owner)
        : base(component, owner)
    {
        First = firstHostNode;
        Last = lastHostNode;
    }

    internal TNode First;

    internal TNode Last;

    internal override TNode FirstHostNode => First;

    internal override TNode LastHostNode => Last;
}
