using System;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace Assimalign.Vue.RuntimeCore;

public static class HtmlRenderer
{
    public static string Render(VirtualNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder();
        RenderNode(builder, node);
        return builder.ToString();
    }

    private static void RenderNode(StringBuilder builder, VirtualNode node)
    {
        switch (node)
        {
            case VirtualText text:
                builder.Append(HtmlEncoder.Default.Encode(text.Content));
                break;
            case VirtualFragment fragment:
                foreach (var child in fragment.Children)
                {
                    RenderNode(builder, child);
                }

                break;
            case VirtualElement element:
                builder.Append('<').Append(element.TagName);

                foreach (var property in element.Properties)
                {
                    if (property.Value is null || property.Value is false || property.Value is VirtualEventHandler)
                    {
                        continue;
                    }

                    var attributeName = string.Equals(property.Key, "className", StringComparison.Ordinal)
                        ? "class"
                        : property.Key;

                    if (property.Value is true)
                    {
                        builder.Append(' ').Append(attributeName);
                        continue;
                    }

                    builder.Append(' ')
                        .Append(attributeName)
                        .Append("=\"")
                        .Append(HtmlEncoder.Default.Encode(FormatPropertyValue(property.Value)))
                        .Append('"');
                }

                builder.Append('>');

                foreach (var child in element.Children)
                {
                    RenderNode(builder, child);
                }

                builder.Append("</").Append(element.TagName).Append('>');
                break;
        }
    }

    private static string FormatPropertyValue(object value)
    {
        return value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
