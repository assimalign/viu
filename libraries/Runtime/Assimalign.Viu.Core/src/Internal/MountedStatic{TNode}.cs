using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedStatic<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedStatic(
        StaticNode value,
        TNode firstHostNode,
        TNode lastHostNode,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        First = firstHostNode;
        Last = lastHostNode;
    }

    internal TNode First;

    internal TNode Last;

    internal override TNode FirstHostNode => First;

    internal override TNode LastHostNode => Last;
}
