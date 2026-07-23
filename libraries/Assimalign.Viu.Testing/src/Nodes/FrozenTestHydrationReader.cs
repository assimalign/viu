using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Reads hydration data from an immutable pre-walk of an in-memory test subtree.
/// </summary>
/// <remarks>
/// Host mutations performed after construction never change this reader. This mirrors a browser
/// or WebView2 adapter that crosses the interop boundary once to snapshot the server tree and
/// detects hydration algorithms that mutate before collecting a complete recovery range.
/// </remarks>
public sealed class FrozenTestHydrationReader : HydrationNodeReader<TestNode>
{
    private readonly Dictionary<TestNode, Frame> _frames =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>Captures the complete subtree rooted at <paramref name="root"/>.</summary>
    /// <param name="root">The hydration container or Teleport target.</param>
    public FrozenTestHydrationReader(TestNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Capture(root, parent: null, nextSibling: null);
    }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.Kind
            : HydrationNodeKind.Other;
    }

    /// <inheritdoc/>
    public override TestNode? FirstChild(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.FirstChild
            : null;
    }

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.NextSibling
            : null;
    }

    /// <inheritdoc/>
    public override TestNode? ParentNode(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.Parent
            : null;
    }

    /// <inheritdoc/>
    public override string ElementTag(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.Tag
            : string.Empty;
    }

    /// <inheritdoc/>
    public override string Data(TestNode node)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            ? frame.Data
            : string.Empty;
    }

    /// <inheritdoc/>
    public override string? Attribute(TestNode node, string name)
    {
        return _frames.TryGetValue(node, out Frame? frame)
            && frame.Attributes is not null
            && frame.Attributes.TryGetValue(name, out string? value)
                ? value
                : null;
    }

    private void Capture(
        TestNode node,
        TestElement? parent,
        TestNode? nextSibling)
    {
        TestElement? element = node as TestElement;
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
            FirstChild = element is { Children.Count: > 0 }
                ? element.Children[0]
                : null,
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

        for (int index = 0; index < element.Children.Count; index++)
        {
            TestNode child = element.Children[index];
            TestNode? following = index + 1 < element.Children.Count
                ? element.Children[index + 1]
                : null;
            Capture(child, element, following);
        }
    }

    private static Dictionary<string, string?>? CaptureAttributes(
        TestElement? element)
    {
        if (element is null || element.Properties.Count == 0)
        {
            return null;
        }

        Dictionary<string, string?> attributes =
            new(element.Properties.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> attribute in element.Properties)
        {
            attributes[attribute.Key] = attribute.Value as string
                ?? Convert.ToString(
                    attribute.Value,
                    CultureInfo.InvariantCulture);
        }

        return attributes;
    }

    private sealed class Frame
    {
        internal required HydrationNodeKind Kind { get; init; }

        internal required TestElement? Parent { get; init; }

        internal required TestNode? FirstChild { get; init; }

        internal required TestNode? NextSibling { get; init; }

        internal required string Tag { get; init; }

        internal required string Data { get; init; }

        internal required Dictionary<string, string?>? Attributes { get; init; }
    }
}
