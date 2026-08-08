namespace Assimalign.Viu;

internal sealed class KeepAliveCacheEntry<TNode>
    where TNode : notnull
{
    internal KeepAliveCacheEntry(
        object key,
        MountedNode<TNode> node,
        string? componentName)
    {
        Key = key;
        Node = node;
        ComponentName = componentName;
    }

    internal object Key;

    internal MountedNode<TNode> Node;

    internal string? ComponentName;
}
