using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Generators.Syntax.CompiledFixtureTests;

internal enum CompiledFixtureNodeKind
{
    Container,
    Element,
    Text,
    Comment,
    Static,
}

/// <summary>Represents one node owned by the DOM-free compiled-fixture host.</summary>
internal sealed class CompiledFixtureNode
{
    internal CompiledFixtureNode(
        CompiledFixtureNodeKind kind,
        string description,
        string? text = null)
    {
        Kind = kind;
        Description = description;
        Text = text;
    }

    internal CompiledFixtureNodeKind Kind { get; }

    internal string Description { get; }

    internal string? Text { get; set; }

    internal CompiledFixtureNode? Parent { get; set; }

    internal List<CompiledFixtureNode> Children { get; } = [];

    internal Dictionary<string, object?> Bindings { get; } =
        new(StringComparer.Ordinal);

    internal ModelBinding? ModelBinding { get; set; }

    internal string DescendantText
    {
        get
        {
            if (Kind is CompiledFixtureNodeKind.Text or CompiledFixtureNodeKind.Static)
            {
                return Text ?? string.Empty;
            }

            if (Kind is CompiledFixtureNodeKind.Comment)
            {
                return string.Empty;
            }

            string result = string.Empty;
            for (int index = 0; index < Children.Count; index++)
            {
                result = string.Concat(result, Children[index].DescendantText);
            }

            return result;
        }
    }
}

/// <summary>
/// Executes the real renderer through host operations while exposing deterministic patch and move
/// observations to the compiled-fixture assertions.
/// </summary>
internal sealed class CompiledFixtureHost : IDisposable
{
    private readonly Queue<Action> _scheduledFlushes = [];
    private readonly IDisposable _schedulerRegistration;
    private bool _isDisposed;

    internal CompiledFixtureHost()
    {
        Scheduler.Reset();
        _schedulerRegistration = Scheduler.UseFlushDispatcher(_scheduledFlushes.Enqueue);
        Container = new CompiledFixtureNode(
            CompiledFixtureNodeKind.Container,
            "fixture root");
    }

    internal CompiledFixtureNode Container { get; }

    internal int MoveCount { get; private set; }

    internal int TextChangeCount { get; private set; }

    internal int BindingPatchCount { get; private set; }

    internal int StaticInsertionCount { get; private set; }

    internal MarkupFormat? LastStaticFormat { get; private set; }

    internal string? LastStaticContent { get; private set; }

    internal Renderer<CompiledFixtureNode> CreateRenderer() =>
        RendererFactory.CreateRenderer(
            new RendererOptions<CompiledFixtureNode>
            {
                Insert = Insert,
                Remove = Remove,
                CreateElement = static name => new CompiledFixtureNode(
                    CompiledFixtureNodeKind.Element,
                    name.ToString()),
                CreateText = static text => new CompiledFixtureNode(
                    CompiledFixtureNodeKind.Text,
                    "text",
                    text),
                CreateComment = static text => new CompiledFixtureNode(
                    CompiledFixtureNodeKind.Comment,
                    "comment",
                    text),
                SetText = SetText,
                ParentNode = static node => node.Parent,
                NextSibling = NextSibling,
                PatchAttribute = PatchBinding,
                InsertStaticContent = InsertStaticContent,
            });

    internal IReadOnlyList<CompiledFixtureNode> FindElements(string localName)
    {
        ArgumentException.ThrowIfNullOrEmpty(localName);
        List<CompiledFixtureNode> matches = [];
        AddMatchingElements(Container, localName, matches);
        return matches.AsReadOnly();
    }

    internal int RunScheduledFlushes()
    {
        int count = 0;
        while (_scheduledFlushes.Count > 0)
        {
            _scheduledFlushes.Dequeue()();
            count++;
        }

        return count;
    }

    internal void ResetOperationCounts()
    {
        MoveCount = 0;
        TextChangeCount = 0;
        BindingPatchCount = 0;
        StaticInsertionCount = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Scheduler.Reset();
        _scheduledFlushes.Clear();
        _schedulerRegistration.Dispose();
        _isDisposed = true;
    }

    private static void AddMatchingElements(
        CompiledFixtureNode node,
        string localName,
        ICollection<CompiledFixtureNode> matches)
    {
        if (node.Kind == CompiledFixtureNodeKind.Element
            && string.Equals(node.Description, localName, StringComparison.Ordinal))
        {
            matches.Add(node);
        }

        for (int index = 0; index < node.Children.Count; index++)
        {
            AddMatchingElements(node.Children[index], localName, matches);
        }
    }

    private static CompiledFixtureNode? NextSibling(CompiledFixtureNode node)
    {
        if (node.Parent is not { } parent)
        {
            return null;
        }

        int index = parent.Children.IndexOf(node);
        return index >= 0 && index + 1 < parent.Children.Count
            ? parent.Children[index + 1]
            : null;
    }

    private void Insert(
        CompiledFixtureNode child,
        CompiledFixtureNode parent,
        CompiledFixtureNode? anchor)
    {
        if (ReferenceEquals(child, anchor))
        {
            return;
        }

        bool isMove = child.Parent is not null;
        child.Parent?.Children.Remove(child);
        int index = anchor is null
            ? parent.Children.Count
            : parent.Children.IndexOf(anchor);
        parent.Children.Insert(index < 0 ? parent.Children.Count : index, child);
        child.Parent = parent;
        if (isMove)
        {
            MoveCount++;
        }
    }

    private static void Remove(CompiledFixtureNode node)
    {
        node.Parent?.Children.Remove(node);
        node.Parent = null;
    }

    private void SetText(CompiledFixtureNode node, string text)
    {
        node.Text = text;
        TextChangeCount++;
    }

    private void PatchBinding(
        CompiledFixtureNode element,
        ElementBinding? previous,
        ElementBinding? current)
    {
        ElementBinding binding = current ?? previous
            ?? throw new InvalidOperationException(
                "A binding patch must carry a previous or current binding.");
        if (current is null)
        {
            element.Bindings.Remove(binding.Name.LocalName);
        }
        else
        {
            element.Bindings[binding.Name.LocalName] = binding.Value;
        }

        BindingPatchCount++;
    }

    private (CompiledFixtureNode First, CompiledFixtureNode Last) InsertStaticContent(
        MarkupFormat format,
        string content,
        CompiledFixtureNode parent,
        CompiledFixtureNode? anchor)
    {
        var node = new CompiledFixtureNode(
            CompiledFixtureNodeKind.Static,
            "static markup",
            content);
        Insert(node, parent, anchor);
        StaticInsertionCount++;
        LastStaticFormat = format;
        LastStaticContent = content;
        return (node, node);
    }
}

/// <summary>Lets the DOM-free test host observe and drive a generated native v-model carrier.</summary>
internal sealed class CompiledFixtureModelDirective : IDirective
{
    internal static CompiledFixtureModelDirective Instance { get; } = new();

    private CompiledFixtureModelDirective()
    {
    }

    /// <inheritdoc />
    public DirectiveHook? Mounted => Apply;

    /// <inheritdoc />
    public DirectiveHook? Updated => Apply;

    private static void Apply(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        _ = value;
        _ = previousValue;
        ((CompiledFixtureNode)element).ModelBinding =
            binding.Value as ModelBinding
            ?? throw new InvalidOperationException(
                "The generated v-model directive did not carry ModelBinding.");
    }
}
