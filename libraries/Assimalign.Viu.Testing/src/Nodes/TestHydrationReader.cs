using System.Globalization;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// The in-memory <see cref="HydrationNodeReader{TNode}"/> — the DOM-free stand-in for the browser's
/// batched snapshot reader. It answers the hydration walk's reads directly off the live
/// <see cref="TestNode"/> tree (the same tree the SSR renderer's output would be parsed into), so a
/// green hydration test against it implies the same adoption walk against the browser DOM. Stateless
/// and shared through <see cref="Instance"/>; reads any node in the tree, so one instance serves the
/// hydration root and every teleport target.
/// </summary>
public sealed class TestHydrationReader : HydrationNodeReader<TestNode>
{
    /// <summary>The shared, stateless reader instance.</summary>
    public static readonly TestHydrationReader Instance = new();

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
    public override TestNode? FirstChild(TestNode node)
        => node is TestElement element && element.Children.Count > 0 ? element.Children[0] : null;

    /// <inheritdoc/>
    public override TestNode? NextSibling(TestNode node)
    {
        if (node.Parent is null)
        {
            return null;
        }
        var siblings = node.Parent.Children;
        var index = siblings.IndexOf(node);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
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
        if (node is not TestElement element || !element.Properties.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }
        return value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
