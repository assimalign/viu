using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeCore;

public sealed class VirtualDomRenderer<TNode>
{
    private readonly IVirtualDomAdapter<TNode> _adapter;
    private MountedNode? _currentRoot;

    public VirtualDomRenderer(IVirtualDomAdapter<TNode> adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public void Render(TNode container, VirtualNode tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (_currentRoot is null)
        {
            _adapter.ClearChildren(container);
            _currentRoot = Mount(tree);
            InsertMountedNode(container, _currentRoot);
            return;
        }

        _currentRoot = PatchNode(container, _currentRoot, tree);
    }

    public void Unmount(TNode container)
    {
        if (_currentRoot is null)
        {
            return;
        }

        RemoveMountedNode(container, _currentRoot);
        _currentRoot = null;
    }

    private MountedNode PatchNode(TNode parent, MountedNode current, VirtualNode next)
    {
        if (!CanPatch(current.Node, next))
        {
            var replacement = Mount(next);
            ReplaceMountedNode(parent, current, replacement);
            return replacement;
        }

        switch (current)
        {
            case MountedTextNode currentTextNode when next is VirtualText nextText:
                var currentText = (VirtualText)currentTextNode.Node;
                if (!string.Equals(currentText.Content, nextText.Content, StringComparison.Ordinal))
                {
                    _adapter.SetText(currentTextNode.NativeNode, nextText.Content);
                }

                currentTextNode.Node = nextText;
                return currentTextNode;

            case MountedElementNode currentElementNode when next is VirtualElement nextElement:
                PatchProperties(currentElementNode.NativeNode, (VirtualElement)currentElementNode.Node, nextElement);
                PatchChildren(currentElementNode.NativeNode, currentElementNode.Children, nextElement.Children);
                currentElementNode.Node = nextElement;
                return currentElementNode;

            case MountedFragmentNode currentFragmentNode when next is VirtualFragment nextFragment:
                PatchChildren(
                    parent,
                    currentFragmentNode.Children,
                    nextFragment.Children,
                    insertBeforeTail: true,
                    tailNode: currentFragmentNode.EndMarker);
                currentFragmentNode.Node = nextFragment;
                return currentFragmentNode;

            default:
                throw new InvalidOperationException($"Unsupported node combination: {current.Node.Kind} -> {next.Kind}.");
        }
    }

    private void PatchProperties(TNode nativeNode, VirtualElement current, VirtualElement next)
    {
        foreach (var property in current.Properties)
        {
            if (!next.Properties.ContainsKey(property.Key))
            {
                _adapter.RemoveProperty(nativeNode, property.Key);
            }
        }

        foreach (var property in next.Properties)
        {
            if (!current.Properties.TryGetValue(property.Key, out var currentValue) ||
                !Equals(currentValue, property.Value))
            {
                _adapter.SetProperty(nativeNode, property.Key, property.Value);
            }
        }
    }

    private void PatchChildren(
        TNode parent,
        List<MountedNode> currentChildren,
        IReadOnlyList<VirtualNode> nextChildren,
        bool insertBeforeTail = false,
        TNode tailNode = default!)
    {
        var sharedCount = Math.Min(currentChildren.Count, nextChildren.Count);

        for (var index = 0; index < sharedCount; index++)
        {
            currentChildren[index] = PatchNode(parent, currentChildren[index], nextChildren[index]);
        }

        for (var index = currentChildren.Count - 1; index >= nextChildren.Count; index--)
        {
            RemoveMountedNode(parent, currentChildren[index]);
            currentChildren.RemoveAt(index);
        }

        for (var index = sharedCount; index < nextChildren.Count; index++)
        {
            var mountedChild = Mount(nextChildren[index]);

            if (insertBeforeTail)
            {
                InsertMountedNode(parent, mountedChild, insertBeforeTail: true, tailNode);
            }
            else
            {
                InsertMountedNode(parent, mountedChild);
            }

            currentChildren.Add(mountedChild);
        }
    }

    private MountedNode Mount(VirtualNode node)
    {
        switch (node)
        {
            case VirtualText text:
                return new MountedTextNode(node, _adapter.CreateText(text.Content));

            case VirtualElement element:
                var nativeElement = _adapter.CreateElement(element.TagName);

                foreach (var property in element.Properties)
                {
                    _adapter.SetProperty(nativeElement, property.Key, property.Value);
                }

                var mountedChildren = new List<MountedNode>(element.Children.Count);
                foreach (var child in element.Children)
                {
                    var mountedChild = Mount(child);
                    mountedChildren.Add(mountedChild);
                    InsertMountedNode(nativeElement, mountedChild);
                }

                return new MountedElementNode(node, nativeElement, mountedChildren);

            case VirtualFragment fragment:
                var fragmentChildren = new List<MountedNode>(fragment.Children.Count);
                foreach (var child in fragment.Children)
                {
                    fragmentChildren.Add(Mount(child));
                }

                return new MountedFragmentNode(
                    node,
                    _adapter.CreateComment("fragment-start"),
                    _adapter.CreateComment("fragment-end"),
                    fragmentChildren);

            default:
                throw new InvalidOperationException($"Unsupported node kind: {node.Kind}.");
        }
    }

    private void InsertMountedNode(TNode parent, MountedNode node, bool insertBeforeTail = false, TNode tailNode = default!)
    {
        foreach (var nativeNode in GetTopLevelNativeNodes(node))
        {
            if (insertBeforeTail)
            {
                _adapter.InsertBefore(parent, nativeNode, tailNode);
            }
            else
            {
                _adapter.AppendChild(parent, nativeNode);
            }
        }
    }

    private void ReplaceMountedNode(TNode parent, MountedNode current, MountedNode replacement)
    {
        if (TryGetFirstNativeNode(current, out var beforeChild))
        {
            InsertMountedNode(parent, replacement, insertBeforeTail: true, beforeChild);
        }
        else
        {
            InsertMountedNode(parent, replacement);
        }

        RemoveMountedNode(parent, current);
    }

    private void RemoveMountedNode(TNode parent, MountedNode node)
    {
        foreach (var nativeNode in GetTopLevelNativeNodes(node))
        {
            _adapter.RemoveChild(parent, nativeNode);
        }

        DestroyMountedNode(node);
    }

    private void DestroyMountedNode(MountedNode node)
    {
        switch (node)
        {
            case MountedTextNode textNode:
                _adapter.DestroyNode(textNode.NativeNode);
                break;

            case MountedElementNode elementNode:
                foreach (var child in elementNode.Children)
                {
                    DestroyMountedNode(child);
                }

                _adapter.DestroyNode(elementNode.NativeNode);
                break;

            case MountedFragmentNode fragmentNode:
                foreach (var child in fragmentNode.Children)
                {
                    DestroyMountedNode(child);
                }

                _adapter.DestroyNode(fragmentNode.StartMarker);
                _adapter.DestroyNode(fragmentNode.EndMarker);
                break;
        }
    }

    private static IEnumerable<TNode> GetTopLevelNativeNodes(MountedNode node)
    {
        switch (node)
        {
            case MountedTextNode textNode:
                yield return textNode.NativeNode;
                yield break;

            case MountedElementNode elementNode:
                yield return elementNode.NativeNode;
                yield break;

            case MountedFragmentNode fragmentNode:
                yield return fragmentNode.StartMarker;

                foreach (var child in fragmentNode.Children)
                {
                    foreach (var nativeNode in GetTopLevelNativeNodes(child))
                    {
                        yield return nativeNode;
                    }
                }

                yield return fragmentNode.EndMarker;
                yield break;
        }
    }

    private static bool TryGetFirstNativeNode(MountedNode node, out TNode nativeNode)
    {
        switch (node)
        {
            case MountedTextNode textNode:
                nativeNode = textNode.NativeNode;
                return true;

            case MountedElementNode elementNode:
                nativeNode = elementNode.NativeNode;
                return true;

            case MountedFragmentNode fragmentNode:
                nativeNode = fragmentNode.StartMarker;
                return true;

            default:
                nativeNode = default!;
                return false;
        }
    }

    private static bool CanPatch(VirtualNode current, VirtualNode next)
    {
        if (!string.Equals(current.Key, next.Key, StringComparison.Ordinal))
        {
            return false;
        }

        return current switch
        {
            VirtualElement currentElement when next is VirtualElement nextElement =>
                string.Equals(currentElement.TagName, nextElement.TagName, StringComparison.Ordinal),
            VirtualText when next is VirtualText => true,
            VirtualFragment when next is VirtualFragment => true,
            _ => false
        };
    }

    private abstract class MountedNode
    {
        protected MountedNode(VirtualNode node)
        {
            Node = node;
        }

        public VirtualNode Node { get; set; }
    }

    private sealed class MountedTextNode : MountedNode
    {
        public MountedTextNode(VirtualNode node, TNode nativeNode)
            : base(node)
        {
            NativeNode = nativeNode;
        }

        public TNode NativeNode { get; }
    }

    private sealed class MountedElementNode : MountedNode
    {
        public MountedElementNode(VirtualNode node, TNode nativeNode, List<MountedNode> children)
            : base(node)
        {
            NativeNode = nativeNode;
            Children = children;
        }

        public TNode NativeNode { get; }

        public List<MountedNode> Children { get; }
    }

    private sealed class MountedFragmentNode : MountedNode
    {
        public MountedFragmentNode(VirtualNode node, TNode startMarker, TNode endMarker, List<MountedNode> children)
            : base(node)
        {
            StartMarker = startMarker;
            EndMarker = endMarker;
            Children = children;
        }

        public TNode StartMarker { get; }

        public TNode EndMarker { get; }

        public List<MountedNode> Children { get; }
    }
}
