using System;
using System.Collections.Generic;
using System.Net;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Parses server-rendered fragments into the host tree consumed by hydration tests.</summary>
/// <remarks>
/// The parser consumes Core's public <see cref="HydrationMarkers"/> vocabulary and preserves its
/// comment data exactly. It is a focused parser for ServerRenderer output, not a general browser
/// HTML parser. Specified by <c>[SSR-6]</c>, <c>[SSR-MARKERS-3]</c>, and <c>[HYD-2]</c>.
/// </remarks>
public static class TestServerMarkup
{
    private static readonly HashSet<string> VoidElements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr",
        };

    private static readonly string[] KnownMarkers =
    [
        HydrationMarkers.FragmentStart,
        HydrationMarkers.FragmentEnd,
        HydrationMarkers.EmptyComment,
        HydrationMarkers.TeleportStart,
        HydrationMarkers.TeleportEnd,
        HydrationMarkers.TeleportAnchor,
    ];

    /// <summary>Parses a server fragment beneath a fresh test container.</summary>
    /// <param name="markup">The WHATWG-serialized fragment.</param>
    /// <param name="containerTag">The diagnostic container tag.</param>
    /// <returns>The populated test container.</returns>
    public static TestElement Parse(string markup, string containerTag = "root")
    {
        ArgumentNullException.ThrowIfNull(markup);
        ArgumentException.ThrowIfNullOrEmpty(containerTag);
        TestElement container = new(new QualifiedName(containerTag));
        Stack<TestElement> stack = new();
        stack.Push(container);
        int position = 0;
        while (position < markup.Length)
        {
            if (markup[position] != '<')
            {
                position = ParseText(markup, position, stack.Peek());
                continue;
            }

            if (StartsWith(markup, position, "<!--"))
            {
                position = ParseComment(markup, position, stack.Peek());
            }
            else if (position + 1 < markup.Length && markup[position + 1] == '/')
            {
                position = ParseCloseTag(markup, position, stack);
            }
            else
            {
                position = ParseOpenTag(markup, position, stack);
            }
        }

        if (stack.Count != 1)
        {
            throw new FormatException(
                $"Unbalanced markup: {stack.Count - 1} element(s) remain open.");
        }

        return container;
    }

    private static int ParseComment(string markup, int position, TestElement parent)
    {
        int end = markup.IndexOf("-->", position + 4, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new FormatException("Unterminated comment.");
        }

        string serialized = markup[position..(end + 3)];
        string content = serialized[("<!--".Length)..^3];
        for (int index = 0; index < KnownMarkers.Length; index++)
        {
            string marker = KnownMarkers[index];
            if (string.Equals(serialized, marker, StringComparison.Ordinal))
            {
                content = marker[("<!--".Length)..^3];
                break;
            }
        }

        Append(parent, new TestComment(content));
        return end + 3;
    }

    private static int ParseCloseTag(
        string markup,
        int position,
        Stack<TestElement> stack)
    {
        int end = markup.IndexOf('>', position + 2);
        if (end < 0)
        {
            throw new FormatException("Unterminated close tag.");
        }

        string tag = markup[(position + 2)..end].Trim();
        if (stack.Count <= 1)
        {
            throw new FormatException($"Unexpected close tag </{tag}>.");
        }

        TestElement open = stack.Pop();
        if (!string.Equals(open.Tag, tag, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                $"Mismatched close tag: expected </{open.Tag}>, found </{tag}>.");
        }

        return end + 1;
    }

    private static int ParseOpenTag(
        string markup,
        int position,
        Stack<TestElement> stack)
    {
        int end = FindOpenTagEnd(markup, position + 1);
        bool selfClosing = IsSelfClosing(markup, position + 1, end);
        int contentEnd = selfClosing ? PreviousNonWhitespace(markup, end - 1) : end;
        string inner = markup[(position + 1)..contentEnd].Trim();
        int spaceIndex = IndexOfWhitespace(inner);
        string tag = spaceIndex < 0 ? inner : inner[..spaceIndex];
        if (tag.Length == 0)
        {
            throw new FormatException("An open tag must have a name.");
        }

        TestElement element = new(new QualifiedName(tag));
        if (spaceIndex >= 0)
        {
            ParseAttributes(inner[spaceIndex..], element);
        }

        Append(stack.Peek(), element);
        if (!selfClosing && !VoidElements.Contains(tag))
        {
            stack.Push(element);
        }

        return end + 1;
    }

    private static int ParseText(string markup, int position, TestElement parent)
    {
        int next = markup.IndexOf('<', position);
        int end = next < 0 ? markup.Length : next;
        Append(parent, new TestText(WebUtility.HtmlDecode(markup[position..end])));
        return end;
    }

    private static void ParseAttributes(string attributes, TestElement element)
    {
        int position = 0;
        while (position < attributes.Length)
        {
            SkipWhitespace(attributes, ref position);
            if (position >= attributes.Length)
            {
                return;
            }

            int nameStart = position;
            while (position < attributes.Length
                && !char.IsWhiteSpace(attributes[position])
                && attributes[position] != '=')
            {
                position++;
            }

            string name = attributes[nameStart..position];
            if (name.Length == 0)
            {
                throw new FormatException("An attribute must have a name.");
            }

            SkipWhitespace(attributes, ref position);
            if (position < attributes.Length && attributes[position] == '=')
            {
                position++;
                SkipWhitespace(attributes, ref position);
                element.Properties[name] = WebUtility.HtmlDecode(
                    ParseAttributeValue(attributes, ref position));
            }
            else
            {
                element.Properties[name] = string.Empty;
            }
        }
    }

    private static string ParseAttributeValue(string attributes, ref int position)
    {
        if (position >= attributes.Length)
        {
            return string.Empty;
        }

        char quote = attributes[position];
        if (quote is '"' or '\'')
        {
            position++;
            int valueStart = position;
            while (position < attributes.Length && attributes[position] != quote)
            {
                position++;
            }

            if (position >= attributes.Length)
            {
                throw new FormatException("Unterminated quoted attribute value.");
            }

            string value = attributes[valueStart..position];
            position++;
            return value;
        }

        int unquotedStart = position;
        while (position < attributes.Length && !char.IsWhiteSpace(attributes[position]))
        {
            position++;
        }

        return attributes[unquotedStart..position];
    }

    private static int FindOpenTagEnd(string markup, int position)
    {
        char quote = '\0';
        for (int index = position; index < markup.Length; index++)
        {
            char character = markup[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
        }

        throw new FormatException("Unterminated open tag.");
    }

    private static bool IsSelfClosing(string markup, int start, int end)
    {
        int index = PreviousNonWhitespace(markup, end - 1);
        return index >= start && markup[index] == '/';
    }

    private static int PreviousNonWhitespace(string value, int position)
    {
        while (position >= 0 && char.IsWhiteSpace(value[position]))
        {
            position--;
        }

        return position;
    }

    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position]))
        {
            position++;
        }
    }

    private static void Append(TestElement parent, TestNode child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
    }

    private static int IndexOfWhitespace(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool StartsWith(string value, int position, string token) =>
        position + token.Length <= value.Length
        && string.CompareOrdinal(value, position, token, 0, token.Length) == 0;
}
