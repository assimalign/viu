using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedSuspense<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedSuspense(
        SuspenseNode value,
        TNode startAnchor,
        TNode endAnchor,
        TNode storageContainer,
        SuspenseBoundary boundary,
        MountedNode<TNode> contentBranch,
        MountedNode<TNode>? fallbackBranch,
        MountedNode<TNode> activeBranch,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        StorageContainer = storageContainer;
        Boundary = boundary;
        ContentBranch = contentBranch;
        FallbackBranch = fallbackBranch;
        ActiveBranch = activeBranch;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal TNode StorageContainer;

    internal SuspenseBoundary Boundary;

    internal MountedNode<TNode> ContentBranch;

    internal MountedNode<TNode>? FallbackBranch;

    internal MountedNode<TNode> ActiveBranch;

    internal SchedulerJob? ResolveJob;

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;
}
