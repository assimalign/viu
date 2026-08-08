using System;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>Reads hydration structure directly from the live in-memory host tree.</summary>
/// <remarks>Specified by <c>[HYD-1]</c> and <c>[HYD-2]</c>.</remarks>
public sealed class TestHydrationReader : HydrationNodeReader<TestNode>
{
    /// <summary>Gets the shared stateless live-tree reader.</summary>
    public static TestHydrationReader Instance { get; } = new();

    private TestHydrationReader()
    {
    }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(TestNode node) => node switch
    {
        TestElement => HydrationNodeKind.Element,
        TestText => HydrationNodeKind.Text,
        TestComment => HydrationNodeKind.Comment,
        _ => HydrationNodeKind.Other,
    };

    /// <inheritdoc/>
    public override TestNode? FirstChild(TestNode node) =>
        node is TestElement { Children.Count: > 0 } element
            ? element.Children[0]
            : null;

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node)
    {
        TestElement? parent = node.Parent;
        if (parent is null)
        {
            return null;
        }

        int index = parent.Children.IndexOf(node);
        return index >= 0 && index + 1 < parent.Children.Count
            ? parent.Children[index + 1]
            : null;
    }

    /// <inheritdoc/>
    public override TestNode? ParentNode(TestNode node) => node.Parent;

    /// <inheritdoc/>
    public override string ElementTag(TestNode node) => ((TestElement)node).Tag;

    /// <inheritdoc/>
    public override string Data(TestNode node) => node switch
    {
        TestText text => text.Text,
        TestComment comment => comment.Text,
        _ => string.Empty,
    };

    /// <inheritdoc/>
    public override string? Attribute(TestNode node, string name)
    {
        if (node is not TestElement element
            || !element.Properties.TryGetValue(name, out object? value)
            || value is null)
        {
            return null;
        }

        return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
