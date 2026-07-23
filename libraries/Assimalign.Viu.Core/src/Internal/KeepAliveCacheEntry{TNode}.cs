namespace Assimalign.Viu;

internal sealed class KeepAliveCacheEntry<TNode>
    where TNode : notnull
{
    internal KeepAliveCacheEntry(
        MountedRenderNode<TNode> node,
        string? componentName)
    {
        Node = node;
        ComponentName = componentName;
    }

    internal MountedRenderNode<TNode> Node;

    internal string? ComponentName;
}
