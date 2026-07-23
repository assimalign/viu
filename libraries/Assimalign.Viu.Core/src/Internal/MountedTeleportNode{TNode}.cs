using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedTeleportNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedTeleportNode(
        ITeleportComponent component,
        TNode startAnchor,
        TNode endAnchor,
        TNode? targetContainer,
        TNode? targetAnchor,
        bool hasTarget,
        bool childrenMounted,
        List<MountedRenderNode<TNode>> children,
        string? elementNamespace,
        ComponentContext? owner)
        : base(component, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        TargetContainer = targetContainer;
        TargetAnchor = targetAnchor;
        HasTarget = hasTarget;
        ChildrenMounted = childrenMounted;
        Children = children;
        ElementNamespace = elementNamespace;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal TNode? TargetContainer;

    internal TNode? TargetAnchor;

    internal bool HasTarget;

    internal bool ChildrenMounted;

    internal List<MountedRenderNode<TNode>> Children;

    internal SchedulerJob? PendingMountJob;

    internal string? ElementNamespace;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
