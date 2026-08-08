using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedKeepAlive<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedKeepAlive(
        KeepAliveNode value,
        TNode startAnchor,
        TNode endAnchor,
        TNode storageContainer,
        MountedNode<TNode> active,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        StartAnchor = startAnchor;
        EndAnchor = endAnchor;
        StorageContainer = storageContainer;
        Active = active;
    }

    internal TNode StartAnchor;

    internal TNode EndAnchor;

    internal TNode StorageContainer;

    internal MountedNode<TNode> Active;

    internal object? ActiveKey;

    internal Dictionary<object, KeepAliveCacheEntry<TNode>> Cache { get; } = [];

    internal LinkedList<object> Recency { get; } = [];

    internal Dictionary<object, LinkedListNode<object>> RecencyNodes { get; } = [];

    internal override TNode FirstHostNode => StartAnchor;

    internal override TNode LastHostNode => EndAnchor;

    internal void Touch(object key)
    {
        if (RecencyNodes.TryGetValue(key, out LinkedListNode<object>? node))
        {
            Recency.Remove(node);
            Recency.AddLast(node);
        }
    }

    internal void Add(object key, MountedNode<TNode> node, string? componentName)
    {
        Cache[key] = new KeepAliveCacheEntry<TNode>(key, node, componentName);
        RecencyNodes[key] = Recency.AddLast(key);
    }

    internal void Remove(object key)
    {
        Cache.Remove(key);
        if (RecencyNodes.Remove(key, out LinkedListNode<object>? node))
        {
            Recency.Remove(node);
        }
    }

    internal bool ReplaceReference(
        MountedNode<TNode> current,
        MountedNode<TNode> replacement)
    {
        if (ReferenceEquals(Active, current))
        {
            Active = replacement;
        }

        foreach (KeepAliveCacheEntry<TNode> entry in Cache.Values)
        {
            if (ReferenceEquals(entry.Node, current))
            {
                entry.Node = replacement;
                return true;
            }
        }

        return false;
    }

    internal void CollectViews(List<MountedComponentView<TNode>> views)
    {
        Renderer<TNode>.CollectViewsForBuiltIn(Active, views);
        foreach (KeepAliveCacheEntry<TNode> entry in Cache.Values)
        {
            if (!ReferenceEquals(entry.Node, Active))
            {
                Renderer<TNode>.CollectViewsForBuiltIn(entry.Node, views);
            }
        }
    }
}
