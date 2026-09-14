using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Core.Tests;

internal enum RendererParityNodeKind
{
    Container,
    Element,
    Text,
    Comment,
}

internal sealed class RendererParityNode
{
    internal RendererParityNode(
        RendererParityNodeKind kind,
        string description,
        string? text = null)
    {
        Kind = kind;
        Description = description;
        Text = text;
    }

    internal RendererParityNodeKind Kind { get; }

    internal string Description { get; }

    internal string? Text { get; set; }

    internal RendererParityNode? Parent { get; set; }

    internal List<RendererParityNode> Children { get; } = [];

    internal Dictionary<string, object?> Bindings { get; } =
        new(StringComparer.Ordinal);

    internal string DescendantText
    {
        get
        {
            if (Kind is RendererParityNodeKind.Text or RendererParityNodeKind.Comment)
            {
                return Text ?? string.Empty;
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

internal sealed class RendererParityHost : IDisposable
{
    private readonly TestSynchronizationContext _synchronizationContext;
    private readonly TestSchedulerPump _schedulerPump;
    private readonly Dictionary<string, RendererParityNode> _teleportTargets =
        new(StringComparer.Ordinal);
    private bool _isDisposed;

    internal RendererParityHost()
    {
        Scheduler.Reset();
        // [V01.01.03.20], [BLT-13]: loaders and flushes share the host's single execution flow.
        _synchronizationContext = TestSynchronizationContext.Install();
        _schedulerPump = TestSchedulerPump.Install(_synchronizationContext);
        Container = new RendererParityNode(
            RendererParityNodeKind.Container,
            "root container");
    }

    internal RendererParityNode Container { get; }

    internal int InsertCount { get; private set; }

    internal int MoveCount { get; private set; }

    internal int RemoveCount { get; private set; }

    internal int TextChangeCount { get; private set; }

    internal int BindingPatchCount { get; private set; }

    internal List<string> BindingPatchNames { get; } = [];

    internal int CommitCount { get; private set; }

    internal int TeleportResolveCount { get; private set; }

    internal Exception? RemovalFailure { get; set; }

    internal Renderer<RendererParityNode> CreateRenderer() =>
        RendererFactory.CreateRenderer(
            new RendererOptions<RendererParityNode>
            {
                Insert = Insert,
                Remove = Remove,
                CreateElement = CreateElement,
                CreateText = CreateText,
                CreateComment = CreateComment,
                SetText = SetText,
                ParentNode = static node => node.Parent,
                NextSibling = NextSibling,
                PatchAttribute = PatchBinding,
                ResolveTeleportTarget = ResolveTeleportTarget,
                Commit = () => CommitCount++,
            });

    internal RendererParityNode CreateTeleportTarget(string selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        var target = new RendererParityNode(
            RendererParityNodeKind.Container,
            $"teleport target {selector}");
        _teleportTargets.Add(selector, target);
        return target;
    }

    internal void ResetOperationCounts()
    {
        InsertCount = 0;
        MoveCount = 0;
        RemoveCount = 0;
        TextChangeCount = 0;
        BindingPatchCount = 0;
        BindingPatchNames.Clear();
        CommitCount = 0;
        TeleportResolveCount = 0;
    }

    internal int RunUntilIdle() => _schedulerPump.RunUntilIdle();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Scheduler.Reset();
        try
        {
            // Consume canceled loads and stale flush callbacks after renderer teardown.
            RunUntilIdle();
        }
        finally
        {
            Scheduler.Reset();
            _schedulerPump.Dispose();
            _isDisposed = true;
            _synchronizationContext.Dispose();
        }
    }

    private void Insert(
        RendererParityNode child,
        RendererParityNode parent,
        RendererParityNode? anchor)
    {
        if (anchor is not null && !ReferenceEquals(anchor.Parent, parent))
        {
            throw new InvalidOperationException(
                "An insertion anchor must belong to the destination parent.");
        }

        if (ReferenceEquals(child, anchor))
        {
            return;
        }

        bool isMove = child.Parent is not null;
        if (child.Parent is { } previousParent)
        {
            previousParent.Children.Remove(child);
        }

        int insertionIndex = anchor is null
            ? parent.Children.Count
            : parent.Children.IndexOf(anchor);
        parent.Children.Insert(insertionIndex, child);
        child.Parent = parent;

        if (isMove)
        {
            MoveCount++;
        }
        else
        {
            InsertCount++;
        }
    }

    private void Remove(RendererParityNode node)
    {
        if (RemovalFailure is { } failure)
        {
            throw failure;
        }

        if (node.Parent is not { } parent)
        {
            return;
        }

        parent.Children.Remove(node);
        node.Parent = null;
        RemoveCount++;
    }

    private static RendererParityNode CreateElement(QualifiedName name) =>
        new(RendererParityNodeKind.Element, name.ToString());

    private static RendererParityNode CreateText(string text) =>
        new(RendererParityNodeKind.Text, "text", text);

    private static RendererParityNode CreateComment(string text) =>
        new(RendererParityNodeKind.Comment, "comment", text);

    private void SetText(RendererParityNode node, string text)
    {
        node.Text = text;
        TextChangeCount++;
    }

    private static RendererParityNode? NextSibling(RendererParityNode node)
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

    private void PatchBinding(
        RendererParityNode element,
        ElementBinding? previousBinding,
        ElementBinding? nextBinding)
    {
        ElementBinding binding = nextBinding ?? previousBinding
            ?? throw new InvalidOperationException(
                "A binding patch must carry a previous or next value.");
        if (nextBinding is null)
        {
            element.Bindings.Remove(binding.Name.LocalName);
        }
        else
        {
            element.Bindings[binding.Name.LocalName] = binding.Value;
        }

        BindingPatchCount++;
        BindingPatchNames.Add(binding.Name.LocalName);
    }

    private RendererParityNode? ResolveTeleportTarget(string selector)
    {
        TeleportResolveCount++;
        return _teleportTargets.TryGetValue(selector, out RendererParityNode? target)
            ? target
            : null;
    }
}
