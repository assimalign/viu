using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Testing;

/// <summary>Serializes an in-memory test subtree into compact diagnostic markup.</summary>
/// <remarks>
/// Values are intentionally not HTML-escaped because this is an assertion aid, not server
/// serialization. Specified by <c>[CONF-3]</c>; production HTML is governed by <c>[SSR-6]</c>.
/// </remarks>
public static class TestNodeSerializer
{
    /// <summary>Serializes a node and all descendants.</summary>
    /// <param name="node">The subtree root.</param>
    /// <param name="indent">The non-negative spaces per depth, or zero for compact output.</param>
    /// <returns>The diagnostic markup.</returns>
    public static string Serialize(TestNode node, int indent = 0)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfNegative(indent);
        StringBuilder builder = new();
        SerializeNode(builder, node, indent, depth: 0);
        return builder.ToString();
    }

    private static void SerializeNode(
        StringBuilder builder,
        TestNode node,
        int indent,
        int depth)
    {
        string padding = indent > 0
            ? new string(' ', indent * depth)
            : string.Empty;
        switch (node)
        {
            case TestText { IsStaticContent: true } staticContent:
                builder.Append(padding).Append(staticContent.Text);
                break;
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
                throw new InvalidOperationException(
                    $"Unknown test node kind '{node.GetType().Name}'.");
        }
    }

    private static void SerializeElement(
        StringBuilder builder,
        TestElement element,
        int indent,
        int depth)
    {
        string padding = indent > 0
            ? new string(' ', indent * depth)
            : string.Empty;
        builder.Append(padding).Append('<').Append(element.Name);
        foreach (KeyValuePair<string, object?> property in element.Properties)
        {
            if (property.Value is null or Delegate)
            {
                continue;
            }

            builder.Append(' ')
                .Append(property.Key)
                .Append("=\"")
                .Append(property.Value)
                .Append('"');
        }

        builder.Append('>');
        for (int index = 0; index < element.Children.Count; index++)
        {
            if (indent > 0)
            {
                builder.AppendLine();
            }

            SerializeNode(builder, element.Children[index], indent, depth + 1);
        }

        if (indent > 0 && element.Children.Count > 0)
        {
            builder.AppendLine().Append(padding);
        }

        builder.Append("</").Append(element.Name).Append('>');
    }
}
