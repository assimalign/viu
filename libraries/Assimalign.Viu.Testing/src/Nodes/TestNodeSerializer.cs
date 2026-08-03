using System;
using System.Text;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Renders a test node subtree to an HTML-like string for snapshot-style assertions.
/// Event-listener and null properties are omitted, and content is emitted verbatim with no HTML
/// encoding — this output is a debugging and assertion aid, not markup for a browser, so escaping it
/// would hide exactly the characters a test is checking. Use
/// <c>Assimalign.Viu.ServerRenderer</c> when real markup is wanted.
/// </summary>
public static class TestNodeSerializer
{
    /// <summary>Serializes <paramref name="node"/> and its subtree.</summary>
    /// <param name="node">The root of the subtree.</param>
    /// <param name="indent">Spaces per depth level; 0 (the default) renders the whole subtree on one line.</param>
    /// <returns>The HTML-like string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    public static string Serialize(TestNode node, int indent = 0)
    {
        ArgumentNullException.ThrowIfNull(node);
        var builder = new StringBuilder();
        SerializeNode(builder, node, indent, depth: 0);
        return builder.ToString();
    }

    private static void SerializeNode(StringBuilder builder, TestNode node, int indent, int depth)
    {
        var padding = indent > 0 ? new string(' ', indent * depth) : string.Empty;
        switch (node)
        {
            case TestText text:
                builder.Append(padding).Append(text.Text);
                break;
            case TestComment comment:
                builder.Append(padding).Append("<!--").Append(comment.Text).Append("-->");
                break;
            case TestElement element:
                SerializeElement(builder, element, indent, depth);
                break;
            default:
                throw new InvalidOperationException($"Unknown test node kind: {node.GetType().Name}.");
        }
    }

    private static void SerializeElement(StringBuilder builder, TestElement element, int indent, int depth)
    {
        var padding = indent > 0 ? new string(' ', indent * depth) : string.Empty;
        builder.Append(padding).Append('<').Append(element.Tag);
        foreach (var (name, value) in element.Properties)
        {
            if (value is null || value is Delegate || TestEventNames.IsListener(name))
            {
                continue;
            }
            builder.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
        }
        builder.Append('>');
        foreach (var child in element.Children)
        {
            if (indent > 0)
            {
                builder.AppendLine();
            }
            SerializeNode(builder, child, indent, depth + 1);
        }
        if (indent > 0 && element.Children.Count > 0)
        {
            builder.AppendLine().Append(padding);
        }
        builder.Append("</").Append(element.Tag).Append('>');
    }
}
