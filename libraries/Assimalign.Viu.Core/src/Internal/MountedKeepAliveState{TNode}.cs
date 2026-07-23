using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class MountedKeepAliveState<TNode>
    where TNode : notnull
{
    internal MountedKeepAliveState(TNode storageContainer)
    {
        StorageContainer = storageContainer;
    }

    internal TNode StorageContainer { get; }

    internal Dictionary<object, KeepAliveCacheEntry<TNode>> Cache { get; } =
        new();

    internal LinkedList<object> Keys { get; } = new();

    internal Dictionary<object, LinkedListNode<object>> KeyNodes { get; } =
        new();

    internal MountedRenderNode<TNode>? ActiveNode;

    internal object? ActiveKey;

    internal bool ActiveIsCached;

    internal void Add(
        object key,
        MountedRenderNode<TNode> node,
        string? componentName)
    {
        Cache[key] = new KeepAliveCacheEntry<TNode>(
            node,
            componentName);
        KeyNodes[key] = Keys.AddLast(key);
    }

    internal void Touch(object key)
    {
        if (!KeyNodes.TryGetValue(
            key,
            out LinkedListNode<object>? node))
        {
            return;
        }

        Keys.Remove(node);
        Keys.AddLast(node);
    }

    internal void Remove(object key)
    {
        Cache.Remove(key);
        if (KeyNodes.Remove(
            key,
            out LinkedListNode<object>? node))
        {
            Keys.Remove(node);
        }
    }
}
