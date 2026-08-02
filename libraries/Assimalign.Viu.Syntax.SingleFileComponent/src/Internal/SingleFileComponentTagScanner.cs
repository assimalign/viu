using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The shared tag-container scanning machinery used by both parse engines: opening-tag and attribute
/// parsing, the nested-markup template boundary, raw-text closing-tag search, and malformed-tag
/// recovery. Extracted from <see cref="VueSingleFileComponentParseEngine"/> so the hybrid
/// <c>.viu</c> container ([V01.01.06.10]) and the <c>.vue</c> compatibility parser ([V01.01.06.09])
/// share one implementation and cannot drift. Boundary rules follow Vue 3.5's SFC tokenizer: an HTML
/// <c>template</c> uses nested markup boundaries while every other root block is raw text until its
/// matching end tag — see
/// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-core/src/tokenizer.ts and
/// https://github.com/vuejs/core/blob/v3.5.34/packages/compiler-sfc/src/parse.ts.
/// </summary>
/// <remarks>
/// One scanner is used for one source string; instances are not thread-safe. The owning engine
/// supplies its own span factory and diagnostic sink so positions and reports stay consistent with
/// that engine's line table.
/// </remarks>
internal sealed class SingleFileComponentTagScanner
{
    private static readonly HashSet<string> HtmlVoidElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "area",
        "base",
        "br",
        "col",
        "embed",
        "hr",
        "img",
        "input",
        "link",
        "meta",
        "param",
        "source",
        "track",
        "wbr",
    };

    private static readonly HashSet<string> HtmlRawTextElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "script",
        "style",
        "textarea",
        "title",
    };

    private readonly string source;
    private readonly Func<int, int, SourceLocation> spanOf;
    private readonly Action<SingleFileComponentErrorCode, SourceLocation> report;

    /// <summary>Creates a scanner over one complete source string.</summary>
    /// <param name="source">The source to scan.</param>
    /// <param name="spanOf">Maps a <c>(startOffset, endOffset)</c> pair to the engine's source location.</param>
    /// <param name="report">Reports a recoverable diagnostic into the engine's error list.</param>
    public SingleFileComponentTagScanner(
        string source,
        Func<int, int, SourceLocation> spanOf,
        Action<SingleFileComponentErrorCode, SourceLocation> report)
    {
        this.source = source;
        this.spanOf = spanOf;
        this.report = report;
    }

    /// <summary>
    /// Parses a top-level opening tag at <paramref name="startOffset"/> (which must point at <c>&lt;</c>),
    /// including its attributes. Malformed tags and attributes are reported
    /// (<see cref="SingleFileComponentErrorCode.MalformedTagBlock"/> /
    /// <see cref="SingleFileComponentErrorCode.MalformedTagAttribute"/> /
    /// <see cref="SingleFileComponentErrorCode.DuplicateTagAttribute"/>) and yield
    /// <see langword="false"/> with a recovery offset.
    /// </summary>
    /// <param name="startOffset">The offset of the opening <c>&lt;</c>.</param>
    /// <param name="header">The parsed header when the tag is well-formed.</param>
    /// <param name="recoveryOffset">The offset scanning should resume at.</param>
    /// <returns><see langword="true"/> when a complete opening tag was parsed.</returns>
    public bool TryParseOpeningTag(int startOffset, out ParsedTagHeader header, out int recoveryOffset)
    {
        header = default;
        recoveryOffset = startOffset + 1;

        var offset = startOffset + 1;
        if (offset >= source.Length || !IsTagNameStartCharacter(source[offset]))
        {
            recoveryOffset = FindMalformedTagRecovery(startOffset);
            report(SingleFileComponentErrorCode.MalformedTagBlock, spanOf(startOffset, recoveryOffset));
            return false;
        }

        var nameStart = offset;
        offset++;
        while (offset < source.Length && IsTagNameCharacter(source[offset]))
        {
            offset++;
        }

        var name = source.Substring(nameStart, offset - nameStart);
        var options = new List<SingleFileComponentBlockOption>();
        var optionNames = new HashSet<string>(StringComparer.Ordinal);

        while (offset < source.Length)
        {
            SkipWhitespace(ref offset);
            if (offset >= source.Length)
            {
                recoveryOffset = source.Length;
                report(SingleFileComponentErrorCode.MalformedTagBlock, spanOf(startOffset, source.Length));
                return false;
            }

            if (source[offset] == '>')
            {
                var endOffset = offset + 1;
                header = new ParsedTagHeader(
                    name,
                    options.ToArray(),
                    startOffset,
                    endOffset,
                    false,
                    spanOf(startOffset, endOffset));
                recoveryOffset = endOffset;
                return true;
            }

            if (source[offset] == '/')
            {
                var slashOffset = offset;
                offset++;
                SkipWhitespace(ref offset);
                if (offset < source.Length && source[offset] == '>')
                {
                    var endOffset = offset + 1;
                    header = new ParsedTagHeader(
                        name,
                        options.ToArray(),
                        startOffset,
                        endOffset,
                        true,
                        spanOf(startOffset, endOffset));
                    recoveryOffset = endOffset;
                    return true;
                }

                recoveryOffset = FindMalformedTagRecovery(slashOffset);
                report(SingleFileComponentErrorCode.MalformedTagBlock, spanOf(slashOffset, recoveryOffset));
                return false;
            }

            if (source[offset] == '<')
            {
                recoveryOffset = offset;
                report(SingleFileComponentErrorCode.MalformedTagBlock, spanOf(startOffset, offset));
                return false;
            }

            var optionStart = offset;
            while (offset < source.Length && IsAttributeNameCharacter(source[offset]))
            {
                offset++;
            }

            if (offset == optionStart)
            {
                recoveryOffset = FindMalformedTagRecovery(offset);
                report(SingleFileComponentErrorCode.MalformedTagAttribute, spanOf(optionStart, recoveryOffset));
                return false;
            }

            var optionName = source.Substring(optionStart, offset - optionStart);
            SkipWhitespace(ref offset);

            string? optionValue = null;
            if (offset < source.Length && source[offset] == '=')
            {
                offset++;
                SkipWhitespace(ref offset);
                if (offset >= source.Length || source[offset] == '>' || source[offset] == '<')
                {
                    recoveryOffset = FindMalformedTagRecovery(offset);
                    report(SingleFileComponentErrorCode.MalformedTagAttribute, spanOf(optionStart, recoveryOffset));
                    return false;
                }

                var quote = source[offset];
                if (quote == '"' || quote == '\'')
                {
                    offset++;
                    var valueStart = offset;
                    while (offset < source.Length && source[offset] != quote)
                    {
                        offset++;
                    }

                    if (offset >= source.Length)
                    {
                        recoveryOffset = source.Length;
                        report(SingleFileComponentErrorCode.MalformedTagAttribute, spanOf(optionStart, source.Length));
                        return false;
                    }

                    optionValue = source.Substring(valueStart, offset - valueStart);
                    offset++;
                }
                else
                {
                    var valueStart = offset;
                    while (offset < source.Length
                        && !char.IsWhiteSpace(source[offset])
                        && source[offset] != '>'
                        && source[offset] != '<')
                    {
                        offset++;
                    }

                    if (offset == valueStart)
                    {
                        recoveryOffset = FindMalformedTagRecovery(offset);
                        report(SingleFileComponentErrorCode.MalformedTagAttribute, spanOf(optionStart, recoveryOffset));
                        return false;
                    }

                    optionValue = source.Substring(valueStart, offset - valueStart);
                }
            }

            var optionEnd = offset;
            var option = new SingleFileComponentBlockOption(
                optionName,
                optionValue,
                spanOf(optionStart, optionEnd));
            options.Add(option);

            if (!optionNames.Add(optionName))
            {
                report(SingleFileComponentErrorCode.DuplicateTagAttribute, option.Location);
            }
        }

        recoveryOffset = source.Length;
        report(SingleFileComponentErrorCode.MalformedTagBlock, spanOf(startOffset, source.Length));
        return false;
    }

    /// <summary>
    /// Finds the closing tag of a raw-text block: the first <c>&lt;/name&gt;</c> at or after
    /// <paramref name="contentStart"/>, with no markup interpretation of the content between.
    /// </summary>
    /// <param name="name">The block's tag name (matched case-insensitively).</param>
    /// <param name="contentStart">The offset content begins at (just past the opening tag).</param>
    /// <param name="contentEnd">The offset content ends at (the <c>&lt;</c> of the closing tag).</param>
    /// <param name="blockEnd">The offset just past the closing tag's <c>&gt;</c>.</param>
    /// <returns><see langword="true"/> when the closing tag was found.</returns>
    public bool TryFindRawClosingTag(string name, int contentStart, out int contentEnd, out int blockEnd)
    {
        var offset = contentStart;
        while (offset < source.Length)
        {
            var candidate = source.IndexOf('<', offset);
            if (candidate < 0)
            {
                break;
            }

            if (TryReadClosingTag(candidate, out var closingName, out var closingEnd)
                && string.Equals(name, closingName, StringComparison.OrdinalIgnoreCase))
            {
                contentEnd = candidate;
                blockEnd = closingEnd;
                return true;
            }

            offset = candidate + 1;
        }

        contentEnd = source.Length;
        blockEnd = source.Length;
        return false;
    }

    /// <summary>
    /// Finds the closing tag of an HTML template block using a lightweight element stack, so end-tag
    /// text inside quoted attributes, comments, and nested raw-text elements cannot close the root.
    /// </summary>
    /// <param name="rootName">The root block's tag name.</param>
    /// <param name="contentStart">The offset content begins at (just past the opening tag).</param>
    /// <param name="contentEnd">The offset content ends at (the <c>&lt;</c> of the closing tag).</param>
    /// <param name="blockEnd">The offset just past the closing tag's <c>&gt;</c>.</param>
    /// <returns><see langword="true"/> when the matching closing tag was found.</returns>
    public bool TryFindTemplateClosingTag(string rootName, int contentStart, out int contentEnd, out int blockEnd)
    {
        var elementNames = new List<string> { rootName };
        var offset = contentStart;

        while (offset < source.Length)
        {
            var candidate = source.IndexOf('<', offset);
            if (candidate < 0)
            {
                break;
            }

            if (StartsWith(candidate, "<!--"))
            {
                var commentEnd = source.IndexOf("-->", candidate + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    break;
                }

                offset = commentEnd + 3;
                continue;
            }

            if (StartsWith(candidate, "<!") || StartsWith(candidate, "<?"))
            {
                var declarationEnd = FindTagEnd(candidate + 2);
                offset = declarationEnd > candidate ? declarationEnd : candidate + 1;
                continue;
            }

            if (TryReadClosingTag(candidate, out var closingName, out var closingEnd))
            {
                var matchingIndex = FindMatchingElement(elementNames, closingName);
                if (matchingIndex == 0)
                {
                    contentEnd = candidate;
                    blockEnd = closingEnd;
                    return true;
                }

                if (matchingIndex > 0)
                {
                    elementNames.RemoveRange(matchingIndex, elementNames.Count - matchingIndex);
                }

                offset = closingEnd;
                continue;
            }

            if (TryReadOpeningMarkupTag(candidate, out var openingName, out var openingEnd, out var isSelfClosing))
            {
                if (!isSelfClosing && !HtmlVoidElementNames.Contains(openingName))
                {
                    if (HtmlRawTextElementNames.Contains(openingName))
                    {
                        if (TryFindRawClosingTag(openingName, openingEnd, out _, out var rawElementEnd))
                        {
                            offset = rawElementEnd;
                            continue;
                        }

                        contentEnd = source.Length;
                        blockEnd = source.Length;
                        return false;
                    }

                    elementNames.Add(openingName);
                }

                offset = openingEnd;
                continue;
            }

            offset = candidate + 1;
        }

        contentEnd = source.Length;
        blockEnd = source.Length;
        return false;
    }

    /// <summary>
    /// Finds the offset just past the first unquoted <c>&gt;</c> at or after
    /// <paramref name="startOffset"/>, or <c>-1</c> when none exists.
    /// </summary>
    /// <param name="startOffset">The offset to scan from.</param>
    /// <returns>The offset just past the <c>&gt;</c>, or <c>-1</c>.</returns>
    public int FindTagEnd(int startOffset)
    {
        char quote = '\0';
        for (var offset = startOffset; offset < source.Length; offset++)
        {
            var current = source[offset];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return offset + 1;
            }
        }

        return -1;
    }

    /// <summary>Whether the source contains <paramref name="value"/> exactly at <paramref name="offset"/>.</summary>
    /// <param name="offset">The offset to test at.</param>
    /// <param name="value">The literal text to test for.</param>
    /// <returns><see langword="true"/> when the text is present.</returns>
    public bool StartsWith(int offset, string value)
    {
        if (offset < 0 || source.Length - offset < value.Length)
        {
            return false;
        }

        return string.CompareOrdinal(source, offset, value, 0, value.Length) == 0;
    }

    /// <summary>
    /// Whether the template options describe an HTML template — no <c>lang</c>, or <c>lang="html"</c>.
    /// A non-HTML template is scanned as raw text, matching Vue 3.5's preprocessed-template handling.
    /// </summary>
    /// <param name="options">The block's parsed options.</param>
    /// <returns><see langword="true"/> when the template content is HTML.</returns>
    public static bool IsHtmlTemplate(SingleFileComponentBlockOption[] options)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Name, "lang", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(option.Value)
                && !string.Equals(option.Value, "html", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryReadClosingTag(int startOffset, out string name, out int endOffset)
    {
        name = string.Empty;
        endOffset = startOffset;

        if (!StartsWith(startOffset, "</"))
        {
            return false;
        }

        var offset = startOffset + 2;
        SkipWhitespace(ref offset);
        if (offset >= source.Length || !IsTagNameStartCharacter(source[offset]))
        {
            return false;
        }

        var nameStart = offset;
        offset++;
        while (offset < source.Length && IsTagNameCharacter(source[offset]))
        {
            offset++;
        }

        if (offset < source.Length && !char.IsWhiteSpace(source[offset]) && source[offset] != '>')
        {
            return false;
        }

        var close = source.IndexOf('>', offset);
        if (close < 0)
        {
            return false;
        }

        name = source.Substring(nameStart, offset - nameStart);
        endOffset = close + 1;
        return true;
    }

    private bool TryReadOpeningMarkupTag(
        int startOffset,
        out string name,
        out int endOffset,
        out bool isSelfClosing)
    {
        name = string.Empty;
        endOffset = startOffset;
        isSelfClosing = false;

        if (startOffset >= source.Length
            || source[startOffset] != '<'
            || startOffset + 1 >= source.Length
            || !IsTagNameStartCharacter(source[startOffset + 1]))
        {
            return false;
        }

        var offset = startOffset + 1;
        var nameStart = offset;
        offset++;
        while (offset < source.Length && IsTagNameCharacter(source[offset]))
        {
            offset++;
        }

        name = source.Substring(nameStart, offset - nameStart);
        char quote = '\0';
        var lastNonWhitespace = offset - 1;
        for (; offset < source.Length; offset++)
        {
            var current = source[offset];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current == '"' || current == '\'')
            {
                quote = current;
                lastNonWhitespace = offset;
                continue;
            }

            if (current == '>')
            {
                isSelfClosing = source[lastNonWhitespace] == '/';
                endOffset = offset + 1;
                return true;
            }

            if (!char.IsWhiteSpace(current))
            {
                lastNonWhitespace = offset;
            }
        }

        return false;
    }

    private int FindMalformedTagRecovery(int startOffset)
    {
        var lineEnd = startOffset;
        while (lineEnd < source.Length && source[lineEnd] != '\r' && source[lineEnd] != '\n')
        {
            lineEnd++;
        }

        var tagEnd = source.IndexOf('>', startOffset);
        if (tagEnd >= 0 && tagEnd < lineEnd)
        {
            return tagEnd + 1;
        }

        return lineEnd > startOffset ? lineEnd : Math.Min(startOffset + 1, source.Length);
    }

    private static int FindMatchingElement(List<string> elementNames, string closingName)
    {
        for (var index = elementNames.Count - 1; index >= 0; index--)
        {
            if (string.Equals(elementNames[index], closingName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void SkipWhitespace(ref int offset)
    {
        while (offset < source.Length && char.IsWhiteSpace(source[offset]))
        {
            offset++;
        }
    }

    private static bool IsTagNameStartCharacter(char value)
        => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');

    private static bool IsTagNameCharacter(char value)
        => IsTagNameStartCharacter(value)
            || (value >= '0' && value <= '9')
            || value == '-'
            || value == '_'
            || value == ':'
            || value == '.';

    private static bool IsAttributeNameCharacter(char value)
        => !char.IsWhiteSpace(value)
            && value != '='
            && value != '>'
            && value != '/'
            && value != '<'
            && value != '"'
            && value != '\'';

    /// <summary>
    /// A parsed top-level opening tag: its name, attributes (as block options), offsets, self-closing
    /// flag, and the opening tag's source span.
    /// </summary>
    public readonly struct ParsedTagHeader
    {
        /// <summary>Creates the parsed header.</summary>
        /// <param name="name">The tag name exactly as authored.</param>
        /// <param name="options">The attributes, in source order, as block options.</param>
        /// <param name="startOffset">The offset of the opening <c>&lt;</c>.</param>
        /// <param name="endOffset">The offset just past the opening tag's <c>&gt;</c>.</param>
        /// <param name="isSelfClosing">Whether the tag is self-closing (<c>/&gt;</c>).</param>
        /// <param name="location">The opening tag's source span.</param>
        public ParsedTagHeader(
            string name,
            SingleFileComponentBlockOption[] options,
            int startOffset,
            int endOffset,
            bool isSelfClosing,
            SourceLocation location)
        {
            Name = name;
            Options = options;
            StartOffset = startOffset;
            EndOffset = endOffset;
            IsSelfClosing = isSelfClosing;
            Location = location;
        }

        /// <summary>The tag name exactly as authored.</summary>
        public string Name { get; }

        /// <summary>The attributes, in source order, as block options.</summary>
        public SingleFileComponentBlockOption[] Options { get; }

        /// <summary>The offset of the opening <c>&lt;</c>.</summary>
        public int StartOffset { get; }

        /// <summary>The offset just past the opening tag's <c>&gt;</c> (where content begins).</summary>
        public int EndOffset { get; }

        /// <summary>Whether the tag is self-closing (<c>/&gt;</c>) — an empty block.</summary>
        public bool IsSelfClosing { get; }

        /// <summary>The opening tag's source span.</summary>
        public SourceLocation Location { get; }
    }
}
