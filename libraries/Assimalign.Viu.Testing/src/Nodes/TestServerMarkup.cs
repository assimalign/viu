using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Parses a server-rendered HTML fragment into the in-memory <see cref="TestNode"/> tree the
/// hydration walker adopts — the DOM-free stand-in for a browser parsing SSR output into real DOM
/// before <c>CreateSSRApp(...).Mount</c>. It understands exactly the vocabulary
/// <c>Assimalign.Viu.ServerRenderer</c> emits: elements with double/single-quoted or bare
/// attributes, void elements (no closing tag), text, and comments — including the hydration markers
/// <c>&lt;!--[--&gt;</c>, <c>&lt;!--]--&gt;</c>, <c>&lt;!----&gt;</c>, and the teleport anchors. Whitespace is
/// preserved verbatim (Vue SSR emits none between nodes), so write fragments as a single line to mirror
/// real output. Intended for hydration tests ([V01.01.07.03]).
/// </summary>
public static class TestServerMarkup
{
    // The WHATWG void elements the SSR renderer emits with no closing tag and no children.
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr",
    };

    /// <summary>
    /// Parses <paramref name="markup"/> into the children of a fresh container element and returns the
    /// container, ready to hand to <see cref="TestRenderer.Hydrate"/>.
    /// </summary>
    /// <param name="markup">The server-rendered HTML fragment.</param>
    /// <param name="containerTag">The container element's tag (default <c>"root"</c>).</param>
    /// <returns>The container holding the parsed server tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markup"/> is null.</exception>
    /// <exception cref="FormatException">The markup is malformed (an unbalanced or unexpected close tag).</exception>
    public static TestElement Parse(string markup, string containerTag = "root")
    {
        ArgumentNullException.ThrowIfNull(markup);
        var container = new TestElement(containerTag, null);
        var stack = new Stack<TestElement>();
        stack.Push(container);
        var position = 0;
        while (position < markup.Length)
        {
            if (markup[position] == '<')
            {
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
            else
            {
                position = ParseText(markup, position, stack.Peek());
            }
        }
        if (stack.Count != 1)
        {
            throw new FormatException($"Unbalanced markup: {stack.Count - 1} element(s) left open.");
        }
        return container;
    }

    private static int ParseComment(string markup, int position, TestElement parent)
    {
        var end = markup.IndexOf("-->", position + 4, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new FormatException("Unterminated comment.");
        }
        var content = markup[(position + 4)..end];
        Append(parent, new TestComment(content));
        return end + 3;
    }

    private static int ParseCloseTag(string markup, int position, Stack<TestElement> stack)
    {
        var end = markup.IndexOf('>', position);
        if (end < 0)
        {
            throw new FormatException("Unterminated close tag.");
        }
        var tag = markup[(position + 2)..end].Trim();
        if (stack.Count <= 1)
        {
            throw new FormatException($"Unexpected close tag </{tag}>.");
        }
        var open = stack.Pop();
        if (!string.Equals(open.Tag, tag, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Mismatched close tag: expected </{open.Tag}>, found </{tag}>.");
        }
        return end + 1;
    }

    private static int ParseOpenTag(string markup, int position, Stack<TestElement> stack)
    {
        var end = markup.IndexOf('>', position);
        if (end < 0)
        {
            throw new FormatException("Unterminated open tag.");
        }
        var selfClosing = markup[end - 1] == '/';
        var inner = markup[(position + 1)..(selfClosing ? end - 1 : end)].Trim();
        var spaceIndex = IndexOfWhitespace(inner);
        var tag = spaceIndex < 0 ? inner : inner[..spaceIndex];
        var element = new TestElement(tag, null);
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
        var next = markup.IndexOf('<', position);
        var end = next < 0 ? markup.Length : next;
        Append(parent, new TestText(markup[position..end]));
        return end;
    }

    private static void ParseAttributes(string attributes, TestElement element)
    {
        var position = 0;
        while (position < attributes.Length)
        {
            while (position < attributes.Length && char.IsWhiteSpace(attributes[position]))
            {
                position++;
            }
            if (position >= attributes.Length)
            {
                break;
            }
            var nameStart = position;
            while (position < attributes.Length
                && !char.IsWhiteSpace(attributes[position])
                && attributes[position] != '=')
            {
                position++;
            }
            var name = attributes[nameStart..position];
            if (name.Length == 0)
            {
                break;
            }
            while (position < attributes.Length && char.IsWhiteSpace(attributes[position]))
            {
                position++;
            }
            if (position < attributes.Length && attributes[position] == '=')
            {
                position++;
                while (position < attributes.Length && char.IsWhiteSpace(attributes[position]))
                {
                    position++;
                }
                element.Properties[name] = ParseAttributeValue(attributes, ref position);
            }
            else
            {
                // A bare boolean attribute (upstream renders these by presence).
                element.Properties[name] = string.Empty;
            }
        }
    }

    private static string ParseAttributeValue(string attributes, ref int position)
    {
        if (position < attributes.Length && (attributes[position] == '"' || attributes[position] == '\''))
        {
            var quote = attributes[position];
            position++;
            var valueStart = position;
            while (position < attributes.Length && attributes[position] != quote)
            {
                position++;
            }
            var value = attributes[valueStart..position];
            if (position < attributes.Length)
            {
                position++;
            }
            return value;
        }
        var unquotedStart = position;
        while (position < attributes.Length && !char.IsWhiteSpace(attributes[position]))
        {
            position++;
        }
        return attributes[unquotedStart..position];
    }

    private static void Append(TestElement parent, TestNode child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
    }

    private static int IndexOfWhitespace(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }
        return -1;
    }

    private static bool StartsWith(string value, int position, string token)
        => position + token.Length <= value.Length
            && string.CompareOrdinal(value, position, token, 0, token.Length) == 0;
}
