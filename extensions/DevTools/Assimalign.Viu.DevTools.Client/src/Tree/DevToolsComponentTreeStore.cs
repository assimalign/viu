using System;
using System.Collections.Generic;

using Assimalign.Viu.DevTools;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>Applies component deltas without rebuilding surviving nodes. Single-threaded. Specified by <c>[DVT-14]</c>.</summary>
public sealed class DevToolsComponentTreeStore
{
    private readonly Dictionary<int, DevToolsComponentNode> _nodes = [];
    private readonly List<DevToolsComponentNode> _roots = [];
    private readonly Dictionary<int, List<DevToolsComponentNode>> _waitingChildren = [];

    /// <summary>Creates an empty connection-local tree.</summary>
    public DevToolsComponentTreeStore() { }
    /// <summary>Gets roots in structural order.</summary>
    public IReadOnlyList<DevToolsComponentNode> Roots => _roots;
    /// <summary>Gets the identity index.</summary>
    public IReadOnlyDictionary<int, DevToolsComponentNode> Nodes => _nodes;

    /// <summary>Applies a mount or update, preserving an existing node and its children.</summary>
    public void Apply(ComponentChangePayload change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_nodes.TryGetValue(change.Identifier, out DevToolsComponentNode? node))
        {
            node = new(change.Identifier, change.Name, change.Key, change.ParentIdentifier);
            _nodes.Add(node.Identifier, node);
        }
        else
        {
            Siblings(node).Remove(node);
            node.Name = change.Name;
            node.Key = change.Key;
            node.ParentIdentifier = change.ParentIdentifier;
        }
        List<DevToolsComponentNode> siblings = Siblings(node);
        siblings.Insert(Math.Clamp(change.Index, 0, siblings.Count), node);
        // Child-first observations retain order independently for each pending parent. [DVT-14]
        if (_waitingChildren.Remove(node.Identifier, out List<DevToolsComponentNode>? children))
        {
            node.ChildNodes.AddRange(children);
        }
    }

    /// <summary>Moves one keyed node within its existing siblings.</summary>
    public void Reorder(int identifier, int index)
    {
        if (!_nodes.TryGetValue(identifier, out DevToolsComponentNode? node))
        {
            return;
        }

        List<DevToolsComponentNode> siblings = Siblings(node);
        siblings.Remove(node);
        siblings.Insert(Math.Clamp(index, 0, siblings.Count), node);
    }

    /// <summary>Removes a component and all remaining descendants.</summary>
    public void Remove(int identifier)
    {
        if (!_nodes.TryGetValue(identifier, out DevToolsComponentNode? node))
        {
            return;
        }

        Siblings(node).Remove(node);
        foreach (DevToolsComponentNode child in node.ChildNodes.ToArray())
        {
            Remove(child.Identifier);
        }
        _nodes.Remove(identifier);
        _waitingChildren.Remove(identifier);
    }

    /// <summary>Replaces an incomplete telemetry tree with an authoritative snapshot.</summary>
    public void Replace(ComponentTreeSnapshotPayload snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Clear();
        AddChildren(snapshot.Roots, null);
    }

    /// <summary>Forgets all identities on a fresh handshake.</summary>
    public void Clear() { _nodes.Clear(); _roots.Clear(); _waitingChildren.Clear(); }

    private void AddChildren(List<ComponentTreeNodePayload> children, int? parent)
    {
        for (int index = 0; index < children.Count; index++)
        {
            ComponentTreeNodePayload child = children[index];
            Apply(new(child.Identifier, parent, index, child.Name, child.Key));
            AddChildren(child.Children, child.Identifier);
        }
    }

    private List<DevToolsComponentNode> Siblings(DevToolsComponentNode node)
    {
        if (node.ParentIdentifier is not int parent)
        {
            return _roots;
        }
        if (_nodes.TryGetValue(parent, out DevToolsComponentNode? owner))
        {
            return owner.ChildNodes;
        }
        if (!_waitingChildren.TryGetValue(parent, out List<DevToolsComponentNode>? siblings))
        {
            _waitingChildren.Add(parent, siblings = []);
        }
        return siblings;
    }
}
