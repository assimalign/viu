using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Provides a small DOM-free implementation of Core's genuine host operation port.</summary>
/// <remarks>
/// Specified by <c>[RND-HOST-1]</c>, <c>[RND-HOST-3]</c>, and <c>[CONF-3]</c>.
/// </remarks>
public sealed class TestVirtualNodeHost : IVirtualNodeHost<TestNode>
{
    private readonly Dictionary<TestNode, TestNode?> _parents = new();
    private readonly Dictionary<TestNode, List<TestNode>> _children = new();

    /// <inheritdoc />
    public TestNode CreateElement(QualifiedName name) => CreateNode(name.ToString());

    /// <inheritdoc />
    public TestNode CreateText(string text) => CreateNode(string.Concat("text:", text));

    /// <inheritdoc />
    public TestNode CreateComment(string text) => CreateNode(string.Concat("comment:", text));

    /// <inheritdoc />
    public TestNode CreateDetachedContainer() => CreateNode("detached-container");

    /// <inheritdoc />
    public TestNode ResolveTarget(string targetIdentifier) =>
        CreateNode(string.Concat("target:", targetIdentifier));

    /// <inheritdoc />
    public void Insert(TestNode child, TestNode parent, TestNode? anchor)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent);
        var children = _children[parent];
        if (anchor is null)
        {
            children.Add(child);
        }
        else
        {
            var index = children.IndexOf(anchor);
            children.Insert(index < 0 ? children.Count : index, child);
        }

        _parents[child] = parent;
    }

    /// <inheritdoc />
    public void Remove(TestNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_parents.TryGetValue(node, out var parent) && parent is not null)
        {
            _children[parent].Remove(node);
        }

        _parents[node] = null;
    }

    /// <inheritdoc />
    public void SetText(TestNode node, string text)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(text);
    }

    /// <inheritdoc />
    public void PatchBinding(TestNode element, ElementBinding? previous, ElementBinding? current)
    {
        ArgumentNullException.ThrowIfNull(element);
    }

    /// <inheritdoc />
    public TestNode? GetParent(TestNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _parents.TryGetValue(node, out var parent) ? parent : null;
    }

    /// <inheritdoc />
    public TestNode? GetNextSibling(TestNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var parent = GetParent(node);
        if (parent is null)
        {
            return null;
        }

        var children = _children[parent];
        var index = children.IndexOf(node);
        return index >= 0 && index + 1 < children.Count
            ? children[index + 1]
            : null;
    }

    private TestNode CreateNode(string description)
    {
        var node = new TestNode(description);
        _parents.Add(node, null);
        _children.Add(node, new List<TestNode>());
        return node;
    }
}
