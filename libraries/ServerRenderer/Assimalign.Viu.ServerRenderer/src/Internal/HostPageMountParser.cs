using System;
using System.Net;

namespace Assimalign.Viu.ServerRenderer;

// A source-offset scanner for the deliberately narrow host-page contract [SSG-3]. It never
// serializes HTML, so published bootstrap and fingerprinted asset references retain their spelling.
internal static class HostPageMountParser
{
    internal static (int ContentStart, int ContentEnd) Find(string hostPage, string mountSelector)
    {
        ValidateSelector(mountSelector);
        string identifier = mountSelector[1..];
        int contentStart = -1;
        int contentEnd = -1;
        string? mountName = null;
        int mountDepth = 0;
        int templateDepth = 0;
        int position = 0;
        while (position < hostPage.Length)
        {
            int tagStart = hostPage.IndexOf('<', position);
            if (tagStart < 0)
            {
                break;
            }

            if (hostPage.AsSpan(tagStart).StartsWith("<!--", StringComparison.Ordinal))
            {
                int commentEnd = hostPage.IndexOf("-->", tagStart + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    throw InvalidPage(mountSelector, "contains an unterminated HTML comment");
                }

                position = commentEnd + 3;
                continue;
            }

            position = tagStart + 1;
            if (position == hostPage.Length)
            {
                break;
            }

            if (hostPage[position] is '!' or '?')
            {
                position = FindTagEnd(hostPage, position, mountSelector) + 1;
                continue;
            }

            bool closing = hostPage[position] == '/';
            if (closing)
            {
                position++;
            }

            int nameStart = position;
            if (position == hostPage.Length || !char.IsAsciiLetter(hostPage[position]))
            {
                continue;
            }

            while (position < hostPage.Length
                && !IsWhitespace(hostPage[position])
                && hostPage[position] is not '/' and not '>')
            {
                position++;
            }

            string name = hostPage[nameStart..position];
            int tagEnd = FindTagEnd(hostPage, position, mountSelector);
            if (closing)
            {
                if (name.Equals("template", StringComparison.OrdinalIgnoreCase) && templateDepth > 0)
                {
                    templateDepth--;
                }
                else if (templateDepth == 0
                    && mountDepth > 0
                    && name.Equals(mountName, StringComparison.OrdinalIgnoreCase))
                {
                    mountDepth--;
                    if (mountDepth == 0)
                    {
                        contentEnd = tagStart;
                    }
                }

                position = tagEnd + 1;
                continue;
            }

            bool isVoid = IsVoid(name);
            bool isRawText = IsRawText(name);
            bool isTemplate = name.Equals("template", StringComparison.OrdinalIgnoreCase);
            if (templateDepth == 0)
            {
                string? elementIdentifier = ReadIdentifier(
                    hostPage, position, tagEnd, mountSelector, out bool selfClosing);
                if (string.Equals(elementIdentifier, identifier, StringComparison.Ordinal))
                {
                    if (contentStart >= 0)
                    {
                        throw InvalidPage(mountSelector, "matches more than one element");
                    }

                    if (isVoid || selfClosing || isRawText || isTemplate)
                    {
                        throw InvalidPage(mountSelector, "must select a non-void container with an explicit closing tag");
                    }

                    mountName = name;
                    contentStart = tagEnd + 1;
                    mountDepth = 1;
                }
                else if (mountDepth > 0
                    && !isVoid
                    && name.Equals(mountName, StringComparison.OrdinalIgnoreCase))
                {
                    // A slash on an HTML non-void start tag does not close that element.
                    mountDepth++;
                }
            }

            position = tagEnd + 1;
            if (isTemplate)
            {
                templateDepth++;
            }
            else if (isRawText)
            {
                position = FindRawTextEnd(hostPage, name, position, mountSelector);
            }
        }

        if (contentStart < 0)
        {
            throw InvalidPage(mountSelector, "does not match a mount container");
        }

        if (contentEnd < 0)
        {
            throw InvalidPage(mountSelector, "has no matching closing tag");
        }

        return (contentStart, contentEnd);
    }

    private static void ValidateSelector(string selector)
    {
        if (selector.Length < 2
            || selector[0] != '#'
            || (!char.IsAsciiLetter(selector[1]) && selector[1] != '_'))
        {
            throw InvalidSelector();
        }

        for (int index = 2; index < selector.Length; index++)
        {
            if (!char.IsAsciiLetterOrDigit(selector[index]) && selector[index] is not '_' and not '-')
            {
                throw InvalidSelector();
            }
        }

        static ArgumentException InvalidSelector() => new(
            "The mount selector must be '#' followed by an ASCII letter or underscore, "
            + "then ASCII letters, digits, underscores, or hyphens. General CSS selectors are unsupported.",
            "mountSelector");
    }

    private static int FindTagEnd(string source, int position, string selector)
    {
        char quote = '\0';
        for (; position < source.Length; position++)
        {
            char character = source[position];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
            }
            else if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return position;
            }
        }

        throw InvalidPage(selector, "contains an unterminated HTML tag or attribute");
    }

    private static string? ReadIdentifier(
        string source,
        int position,
        int tagEnd,
        string selector,
        out bool selfClosing)
    {
        string? identifier = null;
        bool identifierSeen = false;
        selfClosing = false;
        while (position < tagEnd)
        {
            while (position < tagEnd && IsWhitespace(source[position]))
            {
                position++;
            }

            if (position < tagEnd && source[position] == '/')
            {
                position++;
                selfClosing = position == tagEnd;
                continue;
            }

            int nameStart = position;
            while (position < tagEnd && !IsWhitespace(source[position]) && source[position] is not '=' and not '/')
            {
                position++;
            }

            if (position == nameStart)
            {
                position++;
                continue;
            }

            bool isIdentifier = source.AsSpan(nameStart, position - nameStart)
                .Equals("id", StringComparison.OrdinalIgnoreCase);
            while (position < tagEnd && IsWhitespace(source[position]))
            {
                position++;
            }

            string value = string.Empty;
            if (position < tagEnd && source[position] == '=')
            {
                position++;
                while (position < tagEnd && IsWhitespace(source[position]))
                {
                    position++;
                }

                if (position < tagEnd && source[position] is '\'' or '"')
                {
                    char quote = source[position++];
                    int valueStart = position;
                    while (position < tagEnd && source[position] != quote)
                    {
                        position++;
                    }

                    value = source[valueStart..position];
                    position++;
                }
                else
                {
                    int valueStart = position;
                    while (position < tagEnd && !IsWhitespace(source[position]))
                    {
                        position++;
                    }

                    value = source[valueStart..position];
                }
            }

            if (isIdentifier)
            {
                if (identifierSeen)
                {
                    throw InvalidPage(selector, "contains duplicate id attributes on one element");
                }

                identifierSeen = true;
                identifier = WebUtility.HtmlDecode(value);
            }
        }

        return identifier;
    }

    private static int FindRawTextEnd(string source, string name, int position, string selector)
    {
        if (name.Equals("plaintext", StringComparison.OrdinalIgnoreCase))
        {
            return source.Length;
        }

        string closingName = "</" + name;
        while (position < source.Length)
        {
            int closingStart = source.IndexOf(closingName, position, StringComparison.OrdinalIgnoreCase);
            if (closingStart < 0)
            {
                break;
            }

            int nameEnd = closingStart + closingName.Length;
            if (nameEnd < source.Length
                && (IsWhitespace(source[nameEnd]) || source[nameEnd] is '>' or '/'))
            {
                return FindTagEnd(source, nameEnd, selector) + 1;
            }

            position = nameEnd;
        }

        throw InvalidPage(selector, $"contains an unterminated '{name}' element");
    }

    private static bool IsVoid(string name) => name.ToLowerInvariant() is
        "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link"
        or "meta" or "param" or "source" or "track" or "wbr";

    private static bool IsRawText(string name) => name.ToLowerInvariant() is
        "script" or "style" or "textarea" or "title" or "xmp" or "iframe" or "noembed"
        or "noframes" or "noscript" or "plaintext";

    private static bool IsWhitespace(char character) => character is ' ' or '\t' or '\r' or '\n' or '\f';

    private static ArgumentException InvalidPage(string selector, string reason) =>
        new($"The published host page for mount selector '{selector}' {reason}.", "hostPage");
}
