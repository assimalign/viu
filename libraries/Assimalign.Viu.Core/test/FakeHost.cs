using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

internal enum FakeHostNodeKind
{
    Container,
    Element,
    Text,
    Comment,
    Static,
}

internal sealed class FakeHostNode
{
    internal FakeHostNode(int identifier, FakeHostNodeKind kind, string content)
    {
        Identifier = identifier;
        Kind = kind;
        Content = content;
    }

    internal int Identifier { get; }

    internal FakeHostNodeKind Kind { get; }

    internal string Content { get; set; }

    internal FakeHostNode? Parent { get; set; }

    internal List<FakeHostNode> Children { get; } = [];

    internal Dictionary<string, object?> Attributes { get; } =
        new(StringComparer.Ordinal);
}

internal sealed class FakeHost
{
    private int _nextIdentifier;

    internal FakeHost(Action? commit = null)
    {
        Root = Create(FakeHostNodeKind.Container, "root", record: false);
        Options = new RendererOptions<FakeHostNode>
        {
            Insert = Insert,
            Remove = Remove,
            CreateElement = (tag, _) => Create(FakeHostNodeKind.Element, tag),
            CreateText = text => Create(FakeHostNodeKind.Text, text),
            CreateComment = text => Create(FakeHostNodeKind.Comment, text),
            SetText = SetText,
            ParentNode = node => node.Parent,
            NextSibling = NextSibling,
            PatchAttribute = PatchAttribute,
            ResolveTeleportTarget = ResolveTeleportTarget,
            SetScopeIdentifier = (node, scopeIdentifier) =>
                node.Attributes[scopeIdentifier] = string.Empty,
            Commit = commit,
            InsertStaticContent = InsertStaticContent,
            CreateHydrationReader = root =>
                new FakeHydrationNodeReader(root),
        };
    }

    internal FakeHostNode Root { get; }

    internal RendererOptions<FakeHostNode> Options { get; }

    internal List<string> Operations { get; } = [];

    internal FakeHostNode CreateContainer(string name)
    {
        return Create(FakeHostNodeKind.Container, name, record: false);
    }

    internal FakeHostNode CreateServerElement(
        string tag,
        params FakeHostNode[] children)
    {
        FakeHostNode element = Create(
            FakeHostNodeKind.Element,
            tag,
            record: false);
        for (int index = 0; index < children.Length; index++)
        {
            AppendServerChild(element, children[index]);
        }

        return element;
    }

    internal FakeHostNode CreateServerText(string text)
    {
        return Create(FakeHostNodeKind.Text, text, record: false);
    }

    internal void AppendServerChild(
        FakeHostNode parent,
        FakeHostNode child)
    {
        child.Parent?.Children.Remove(child);
        parent.Children.Add(child);
        child.Parent = parent;
    }

    internal string Text(FakeHostNode node)
    {
        if (node.Children.Count == 0)
        {
            return node.Content;
        }

        string result = string.Empty;
        for (int index = 0; index < node.Children.Count; index++)
        {
            result += Text(node.Children[index]);
        }

        return result;
    }

    private FakeHostNode Create(
        FakeHostNodeKind kind,
        string content,
        bool record = true)
    {
        FakeHostNode node = new(++_nextIdentifier, kind, content);
        if (record)
        {
            Operations.Add($"create:{kind}:{node.Identifier}:{content}");
        }

        return node;
    }

    private void Insert(
        FakeHostNode child,
        FakeHostNode parent,
        FakeHostNode? anchor)
    {
        child.Parent?.Children.Remove(child);
        int index = anchor is null
            ? parent.Children.Count
            : parent.Children.IndexOf(anchor);
        if (index < 0)
        {
            throw new InvalidOperationException("The insertion anchor is not a child of the parent.");
        }

        parent.Children.Insert(index, child);
        child.Parent = parent;
        Operations.Add(
            $"insert:{child.Identifier}:{parent.Identifier}:{anchor?.Identifier.ToString() ?? "end"}");
    }

    private void Remove(FakeHostNode node)
    {
        node.Parent?.Children.Remove(node);
        node.Parent = null;
        Operations.Add($"remove:{node.Identifier}");
    }

    private void SetText(FakeHostNode node, string text)
    {
        string previous = node.Content;
        node.Content = text;
        Operations.Add($"text:{node.Identifier}:{previous}:{text}");
    }

    private FakeHostNode? NextSibling(FakeHostNode node)
    {
        if (node.Parent is null)
        {
            return null;
        }

        int index = node.Parent.Children.IndexOf(node);
        return index >= 0 && index + 1 < node.Parent.Children.Count
            ? node.Parent.Children[index + 1]
            : null;
    }

    private FakeHostNode? ResolveTeleportTarget(object target)
    {
        if (target is not string selector
            || selector.Length < 2
            || selector[0] != '#')
        {
            return null;
        }

        return FindElementByIdentifier(Root, selector[1..]);
    }

    private static FakeHostNode? FindElementByIdentifier(
        FakeHostNode node,
        string identifier)
    {
        if (node.Kind == FakeHostNodeKind.Element
            && node.Attributes.TryGetValue("id", out object? value)
            && string.Equals(value as string, identifier, StringComparison.Ordinal))
        {
            return node;
        }

        for (int index = 0; index < node.Children.Count; index++)
        {
            FakeHostNode? match =
                FindElementByIdentifier(node.Children[index], identifier);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private void PatchAttribute(
        FakeHostNode element,
        string elementTag,
        string attributeName,
        object? previousValue,
        object? nextValue,
        string? elementNamespace)
    {
        if (nextValue is null)
        {
            element.Attributes.Remove(attributeName);
        }
        else
        {
            element.Attributes[attributeName] = nextValue;
        }

        Operations.Add(
            $"attribute:{element.Identifier}:{attributeName}:{previousValue ?? "null"}:{nextValue ?? "null"}");
    }

    private (FakeHostNode First, FakeHostNode Last) InsertStaticContent(
        string content,
        FakeHostNode parent,
        FakeHostNode? anchor,
        string? elementNamespace)
    {
        string[] segments = content.Split('|');
        FakeHostNode? first = null;
        FakeHostNode? last = null;
        for (int index = 0; index < segments.Length; index++)
        {
            FakeHostNode node = Create(FakeHostNodeKind.Static, segments[index]);
            Insert(node, parent, anchor);
            first ??= node;
            last = node;
        }

        return (first!, last!);
    }
}

internal sealed class FakeHydrationNodeReader :
    HydrationNodeReader<FakeHostNode>
{
    private readonly Dictionary<FakeHostNode, FakeHostNode?> _firstChildren = [];
    private readonly Dictionary<FakeHostNode, FakeHostNode?> _nextSiblings = [];
    private readonly Dictionary<FakeHostNode, FakeHostNode?> _parents = [];

    internal FakeHydrationNodeReader(FakeHostNode root)
    {
        Capture(root, parent: null);
    }

    public override HydrationNodeKind Kind(FakeHostNode node)
    {
        return node.Kind switch
        {
            FakeHostNodeKind.Element => HydrationNodeKind.Element,
            FakeHostNodeKind.Text => HydrationNodeKind.Text,
            FakeHostNodeKind.Comment => HydrationNodeKind.Comment,
            _ => HydrationNodeKind.Other,
        };
    }

    public override FakeHostNode? FirstChild(FakeHostNode node)
    {
        return _firstChildren[node];
    }

    public override FakeHostNode? NextSibling(FakeHostNode node)
    {
        return _nextSiblings[node];
    }

    public override FakeHostNode? ParentNode(FakeHostNode node)
    {
        return _parents[node];
    }

    public override string ElementTag(FakeHostNode node)
    {
        return node.Content;
    }

    public override string Data(FakeHostNode node)
    {
        return node.Content;
    }

    public override string? Attribute(
        FakeHostNode node,
        string name)
    {
        return node.Attributes.TryGetValue(name, out object? value)
            ? value as string ?? value?.ToString()
            : null;
    }

    private void Capture(
        FakeHostNode node,
        FakeHostNode? parent)
    {
        _parents.Add(node, parent);
        _firstChildren.Add(
            node,
            node.Children.Count == 0
                ? null
                : node.Children[0]);
        for (int index = 0; index < node.Children.Count; index++)
        {
            FakeHostNode child = node.Children[index];
            _nextSiblings.Add(
                child,
                index + 1 < node.Children.Count
                    ? node.Children[index + 1]
                    : null);
            Capture(child, node);
        }

        _nextSiblings.TryAdd(node, null);
    }
}
