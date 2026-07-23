using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedFragmentNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedFragmentNode(
        IFragmentComponent component,
        TNode startAnchor,
        TNode endAnchor,
        List<MountedRenderNode<TNode>> children,
        ComponentContext? owner)
        : base(component, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        Children = children;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal List<MountedRenderNode<TNode>> Children;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
