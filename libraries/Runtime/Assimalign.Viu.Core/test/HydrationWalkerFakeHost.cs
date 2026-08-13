using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

internal sealed class HydrationWalkerFakeHost
{
    private readonly Dictionary<string, HydrationWalkerHostNode> _targets =
        new(StringComparer.Ordinal);
    private int _nextIdentifier;

    internal HydrationWalkerFakeHost()
    {
        Root = CreateServerElement("root");
        Options = new RendererOptions<HydrationWalkerHostNode>
        {
            Insert = Insert,
            Remove = Remove,
            CreateElement = CreateClientElement,
            CreateText = CreateClientText,
            CreateComment = CreateClientComment,
            SetText = SetText,
            ParentNode = node => node.Parent,
            NextSibling = NextSibling,
            PatchAttribute = PatchAttribute,
            ResolveTeleportTarget = ResolveTarget,
            InsertStaticContent = InsertStaticContent,
            CreateHydrationReader = _ => new Reader(),
        };
    }

    internal HydrationWalkerHostNode Root { get; }

    internal RendererOptions<HydrationWalkerHostNode> Options { get; }

    internal List<string> Operations { get; } = [];

    internal int ClientCreationCount { get; private set; }

    internal HydrationWalkerHostNode CreateServerElement(
        string tag,
        params HydrationWalkerHostNode[] children)
    {
        HydrationWalkerHostNode node = CreateNode(HydrationNodeKind.Element, tag);
        for (int index = 0; index < children.Length; index++)
        {
            AppendServerChild(node, children[index]);
        }

        return node;
    }

    internal HydrationWalkerHostNode CreateServerText(string text) =>
        CreateNode(HydrationNodeKind.Text, text);

    internal HydrationWalkerHostNode CreateServerComment(string data) =>
        CreateNode(HydrationNodeKind.Comment, data);

    internal void AppendServerChild(
        HydrationWalkerHostNode parent,
        HydrationWalkerHostNode child)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = parent;
        parent.Children.Add(child);
    }

    internal void RegisterTarget(string identifier, HydrationWalkerHostNode target)
    {
        _targets.Add(identifier, target);
    }

    private HydrationWalkerHostNode CreateNode(HydrationNodeKind kind, string data) =>
        new(checked(++_nextIdentifier), kind, data);

    private HydrationWalkerHostNode CreateClientElement(QualifiedName name)
    {
        ClientCreationCount++;
        HydrationWalkerHostNode node = CreateNode(HydrationNodeKind.Element, name.LocalName);
        Operations.Add($"create:element:{node.Identifier}:{name}");
        return node;
    }

    private HydrationWalkerHostNode CreateClientText(string text)
    {
        ClientCreationCount++;
        HydrationWalkerHostNode node = CreateNode(HydrationNodeKind.Text, text);
        Operations.Add($"create:text:{node.Identifier}:{text}");
        return node;
    }

    private HydrationWalkerHostNode CreateClientComment(string text)
    {
        ClientCreationCount++;
        HydrationWalkerHostNode node = CreateNode(HydrationNodeKind.Comment, text);
        Operations.Add($"create:comment:{node.Identifier}:{text}");
        return node;
    }

    private void Insert(
        HydrationWalkerHostNode child,
        HydrationWalkerHostNode parent,
        HydrationWalkerHostNode? anchor)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = parent;
        int index = anchor is null
            ? parent.Children.Count
            : parent.Children.IndexOf(anchor);
        if (index < 0)
        {
            index = parent.Children.Count;
        }

        parent.Children.Insert(index, child);
        Operations.Add(
            $"insert:{child.Identifier}:{parent.Identifier}:{anchor?.Identifier ?? 0}");
    }

    private void Remove(HydrationWalkerHostNode node)
    {
        node.Parent?.Children.Remove(node);
        node.Parent = null;
        Operations.Add($"remove:{node.Identifier}");
    }

    private void SetText(HydrationWalkerHostNode node, string text)
    {
        string previous = node.Data;
        node.Data = text;
        Operations.Add($"text:{node.Identifier}:{previous}:{text}");
    }

    private static HydrationWalkerHostNode? NextSibling(HydrationWalkerHostNode node)
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

    private void PatchAttribute(
        HydrationWalkerHostNode element,
        ElementBinding? previous,
        ElementBinding? next)
    {
        ElementBinding? binding = next ?? previous;
        if (binding is null)
        {
            return;
        }

        Operations.Add(
            $"binding:{element.Identifier}:{binding.Kind}:{binding.Name}");
        if (binding.Kind != ElementBindingKind.Attribute)
        {
            return;
        }

        string name = binding.Name.ToString();
        if (next?.Value is null)
        {
            element.Attributes.Remove(name);
            return;
        }

        element.Attributes[name] = next.Value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
            : next.Value.ToString() ?? string.Empty;
    }

    private HydrationWalkerHostNode? ResolveTarget(string identifier) =>
        _targets.TryGetValue(identifier, out HydrationWalkerHostNode? target)
            ? target
            : null;

    private (HydrationWalkerHostNode First, HydrationWalkerHostNode Last) InsertStaticContent(
        MarkupFormat format,
        string content,
        HydrationWalkerHostNode container,
        HydrationWalkerHostNode? anchor)
    {
        _ = format;
        HydrationWalkerHostNode node = CreateClientText(content);
        Insert(node, container, anchor);
        return (node, node);
    }

    private sealed class Reader : HydrationNodeReader<HydrationWalkerHostNode>
    {
        public override HydrationNodeKind Kind(HydrationWalkerHostNode node) => node.Kind;

        public override HydrationWalkerHostNode? FirstChild(HydrationWalkerHostNode node) =>
            node.Children.Count > 0 ? node.Children[0] : null;

        public override HydrationWalkerHostNode? NextSibling(HydrationWalkerHostNode node) =>
            HydrationWalkerFakeHost.NextSibling(node);

        public override HydrationWalkerHostNode? ParentNode(HydrationWalkerHostNode node) =>
            node.Parent;

        public override string ElementTag(HydrationWalkerHostNode node) => node.Data;

        public override string Data(HydrationWalkerHostNode node) => node.Data;

        public override string? Attribute(HydrationWalkerHostNode node, string name) =>
            node.Attributes.TryGetValue(name, out string? value) ? value : null;
    }
}

internal sealed class HydrationWalkerHostNode
{
    internal HydrationWalkerHostNode(
        int identifier,
        HydrationNodeKind kind,
        string data)
    {
        Identifier = identifier;
        Kind = kind;
        Data = data;
    }

    internal int Identifier { get; }

    internal HydrationNodeKind Kind { get; }

    internal string Data { get; set; }

    internal HydrationWalkerHostNode? Parent { get; set; }

    internal List<HydrationWalkerHostNode> Children { get; } = [];

    internal Dictionary<string, string> Attributes { get; } =
        new(StringComparer.Ordinal);
}
