using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>Reads hydration data from an immutable pre-walk of a test subtree.</summary>
/// <remarks>
/// Host mutations after construction cannot alter the snapshot, matching the one-read browser
/// host contract. Specified by <c>[RND-IO-1]</c> and <c>[HYD-2]</c>.
/// </remarks>
public sealed class FrozenTestHydrationReader : HydrationNodeReader<TestNode>
{
    private readonly Dictionary<TestNode, Frame> _frames =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>Captures the complete subtree rooted at the supplied host node.</summary>
    /// <param name="root">The hydration container or teleport target.</param>
    public FrozenTestHydrationReader(TestNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Capture(root, parent: null, nextSibling: null);
    }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame)
            ? frame.Kind
            : HydrationNodeKind.Other;

    /// <inheritdoc/>
    public override TestNode? FirstChild(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame) ? frame.FirstChild : null;

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame) ? frame.NextSibling : null;

    /// <inheritdoc/>
    public override TestNode? ParentNode(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame) ? frame.Parent : null;

    /// <inheritdoc/>
    public override string ElementTag(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame) ? frame.Tag : string.Empty;

    /// <inheritdoc/>
    public override string Data(TestNode node) =>
        _frames.TryGetValue(node, out Frame? frame) ? frame.Data : string.Empty;

    /// <inheritdoc/>
    public override string? Attribute(TestNode node, string name) =>
        _frames.TryGetValue(node, out Frame? frame)
        && frame.Attributes is not null
        && frame.Attributes.TryGetValue(name, out string? value)
            ? value
            : null;

    private void Capture(TestNode node, TestElement? parent, TestNode? nextSibling)
    {
        TestElement? element = node as TestElement;
        _frames[node] = new Frame
        {
            Kind = TestHydrationReader.Instance.Kind(node),
            Parent = parent,
            FirstChild = element is { Children.Count: > 0 } ? element.Children[0] : null,
            NextSibling = nextSibling,
            Tag = element?.Tag ?? string.Empty,
            Data = TestHydrationReader.Instance.Data(node),
            Attributes = CaptureAttributes(element),
        };

        if (element is null)
        {
            return;
        }

        for (int index = 0; index < element.Children.Count; index++)
        {
            TestNode? following = index + 1 < element.Children.Count
                ? element.Children[index + 1]
                : null;
            Capture(element.Children[index], element, following);
        }
    }

    private static Dictionary<string, string?>? CaptureAttributes(TestElement? element)
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
                ?? Convert.ToString(attribute.Value, CultureInfo.InvariantCulture);
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
