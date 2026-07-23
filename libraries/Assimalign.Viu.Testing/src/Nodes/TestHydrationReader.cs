using System;
using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Reads hydration structure directly from the live in-memory test tree.
/// </summary>
/// <remarks>
/// The reader is stateless and can inspect the main hydration container and registered Teleport
/// targets. Use <see cref="FrozenTestHydrationReader"/> to reproduce a browser adapter's
/// read-once snapshot semantics.
/// </remarks>
public sealed class TestHydrationReader : HydrationNodeReader<TestNode>
{
    /// <summary>Gets the shared stateless reader.</summary>
    public static TestHydrationReader Instance { get; } = new();

    private TestHydrationReader()
    {
    }

    /// <inheritdoc/>
    public override HydrationNodeKind Kind(TestNode node)
    {
        return node switch
        {
            TestElement => HydrationNodeKind.Element,
            TestText => HydrationNodeKind.Text,
            TestComment => HydrationNodeKind.Comment,
            _ => HydrationNodeKind.Other,
        };
    }

    /// <inheritdoc/>
    public override TestNode? FirstChild(TestNode node)
    {
        return node is TestElement { Children.Count: > 0 } element
            ? element.Children[0]
            : null;
    }

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node)
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

    /// <inheritdoc/>
    public override TestNode? ParentNode(TestNode node)
    {
        return node.Parent;
    }

    /// <inheritdoc/>
    public override string ElementTag(TestNode node)
    {
        return ((TestElement)node).Tag;
    }

    /// <inheritdoc/>
    public override string Data(TestNode node)
    {
        return node switch
        {
            TestText text => text.Text,
            TestComment comment => comment.Text,
            _ => string.Empty,
        };
    }

    /// <inheritdoc/>
    public override string? Attribute(TestNode node, string name)
    {
        if (node is not TestElement element
            || !element.Properties.TryGetValue(name, out object? value)
            || value is null)
        {
            return null;
        }

        return value as string
            ?? Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
