using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedTeleport<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedTeleport(
        TeleportNode value,
        TNode startAnchor,
        TNode endAnchor,
        TNode? targetContainer,
        TNode? targetAnchor,
        bool hasTarget,
        List<MountedNode<TNode>> children,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        TargetContainer = targetContainer;
        TargetAnchor = targetAnchor;
        HasTarget = hasTarget;
        Children = children;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal TNode? TargetContainer;

    internal TNode? TargetAnchor;

    internal bool HasTarget;

    internal bool ChildrenMounted;

    internal List<MountedNode<TNode>> Children;

    internal SchedulerJob? PendingJob;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
