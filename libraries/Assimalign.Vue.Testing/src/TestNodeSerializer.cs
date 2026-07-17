using System;
using System.Text;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.Testing;

/// <summary>
/// Renders a test node subtree to an HTML-like string for snapshot-style assertions — the C#
/// port of <c>serialize</c> in <c>@vue/runtime-test</c>
/// (<c>packages/runtime-test/src/serialize.ts</c>). Event-listener and null props are omitted;
/// content is emitted verbatim (no HTML encoding), matching upstream.
/// </summary>
public static class TestNodeSerializer
{
    /// <summary>Serializes <paramref name="node"/> and its subtree.</summary>
    /// <param name="node">The root of the subtree.</param>
    /// <param name="indent">Spaces per depth level; 0 renders one line (upstream default).</param>
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
            if (value is null || value is Delegate || VirtualNodeFactory.IsEventListenerName(name))
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
