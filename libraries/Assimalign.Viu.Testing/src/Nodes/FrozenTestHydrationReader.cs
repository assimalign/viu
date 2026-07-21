using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// A hydration reader that mirrors the <b>browser</b>'s snapshot semantics in-memory — the DOM-free
/// stand-in for <c>BrowserHydrationReader</c>, not the live-tree <see cref="TestHydrationReader"/>. It
/// takes an <b>immutable pre-walk</b> of the <see cref="TestNode"/> subtree at construction and answers
/// every read from that frozen copy, so it <b>never</b> reflects an <c>Insert</c>/<c>Remove</c>/<c>SetText</c>
/// the walker performs afterward — exactly like the batched JS snapshot the browser reads once per root.
/// <para>
/// Its purpose is regression coverage: a walker that re-reads structure after mutating (rather than
/// reading-before-mutate) works against the live reader but breaks against a frozen snapshot — the bug
/// class the browser hits. Wire it through <see cref="TestRenderer"/>'s snapshot-semantics mode
/// ([V01.01.07.03]).
/// </para>
/// </summary>
public sealed class FrozenTestHydrationReader : HydrationNodeReader<TestNode>
{
    private readonly Dictionary<TestNode, Frame> _frames = new(ReferenceEqualityComparer.Instance);

    /// <summary>Pre-walks and freezes the subtree rooted at <paramref name="root"/>.</summary>
    /// <param name="root">The subtree root (the hydration container, or a teleport target).</param>
    public FrozenTestHydrationReader(TestNode root)
    {
        Capture(root, parent: null, nextSibling: null);
    }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.Kind : HydrationNodeKind.Other;

    /// <inheritdoc/>
    public override TestNode? FirstChild(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.FirstChild : null;

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.NextSibling : null;

    /// <inheritdoc/>
    public override TestNode? ParentNode(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.Parent : null;

    /// <inheritdoc/>
    public override string ElementTag(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.Tag : string.Empty;

    /// <inheritdoc/>
    public override string Data(TestNode node)
        => _frames.TryGetValue(node, out var frame) ? frame.Data : string.Empty;

    /// <inheritdoc/>
    public override string? Attribute(TestNode node, string name)
        => _frames.TryGetValue(node, out var frame) && frame.Attributes is { } attributes
            && attributes.TryGetValue(name, out var value)
            ? value
            : null;

    private void Capture(TestNode node, TestElement? parent, TestNode? nextSibling)
    {
        var element = node as TestElement;
        _frames[node] = new Frame
        {
            Kind = node switch
            {
                TestElement => HydrationNodeKind.Element,
                TestText => HydrationNodeKind.Text,
                TestComment => HydrationNodeKind.Comment,
                _ => HydrationNodeKind.Other,
            },
            Parent = parent,
            FirstChild = element is { Children.Count: > 0 } ? element.Children[0] : null,
            NextSibling = nextSibling,
            Tag = element?.Tag ?? string.Empty,
            Data = node switch
            {
                TestText text => text.Text,
                TestComment comment => comment.Text,
                _ => string.Empty,
            },
            Attributes = CaptureAttributes(element),
        };
        if (element is null)
        {
            return;
        }
        for (var index = 0; index < element.Children.Count; index++)
        {
            var child = element.Children[index];
            var sibling = index + 1 < element.Children.Count ? element.Children[index + 1] : null;
            Capture(child, element, sibling);
        }
    }

    private static Dictionary<string, string?>? CaptureAttributes(TestElement? element)
    {
        if (element is null || element.Properties.Count == 0)
        {
            return null;
        }
        var attributes = new Dictionary<string, string?>(element.Properties.Count, System.StringComparer.Ordinal);
        foreach (var (name, value) in element.Properties)
        {
            attributes[name] = value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        return attributes;
    }

    private sealed class Frame
    {
        public required HydrationNodeKind Kind { get; init; }

        public required TestElement? Parent { get; init; }

        public required TestNode? FirstChild { get; init; }

        public required TestNode? NextSibling { get; init; }

        public required string Tag { get; init; }

        public required string Data { get; init; }

        public required Dictionary<string, string?>? Attributes { get; init; }
    }
}
