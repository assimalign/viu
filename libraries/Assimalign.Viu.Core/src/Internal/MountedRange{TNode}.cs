using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedRange<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedRange(
        VirtualNode value,
        TNode startAnchor,
        TNode endAnchor,
        List<MountedNode<TNode>> children,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        Children = children;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal List<MountedNode<TNode>> Children;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
