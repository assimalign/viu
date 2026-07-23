namespace Assimalign.Viu;

internal sealed class MountedSuspenseState<TNode>
    where TNode : notnull
{
    internal MountedSuspenseState(
        TNode storageContainer,
        SuspenseBoundary boundary,
        MountedRenderNode<TNode> contentBranch)
    {
        StorageContainer = storageContainer;
        Boundary = boundary;
        ContentBranch = contentBranch;
    }

    internal TNode StorageContainer { get; }

    internal SuspenseBoundary Boundary { get; }

    internal MountedRenderNode<TNode> ContentBranch;

    internal MountedRenderNode<TNode>? FallbackBranch;

    internal bool IsShowingFallback;
}
