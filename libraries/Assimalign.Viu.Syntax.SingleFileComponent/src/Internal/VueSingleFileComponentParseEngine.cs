using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The recoverable tag-container scanner behind <see cref="VueSingleFileComponentParser"/>.
/// </summary>
/// <remarks>
/// One engine is used for one source string. Instances are not thread-safe. Root blocks other than an
/// HTML <c>template</c> are scanned as raw text, matching Vue 3.5's SFC tokenizer. Template boundaries
/// are found with a lightweight HTML stack so end-tag text in attributes, comments, and nested raw-text
/// elements cannot close the root template.
/// </remarks>
internal sealed class VueSingleFileComponentParseEngine
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
    private readonly int[] lineStarts;
    private readonly List<SingleFileComponentError> errors = new();

    /// <summary>Creates a parser engine for one complete <c>.vue</c> source string.</summary>
    /// <param name="source">The source to scan.</param>
    public VueSingleFileComponentParseEngine(string source)
    {
        this.source = source;
        this.lineStarts = BuildLineStarts(source);
    }

    /// <summary>Scans the source and returns all recognized top-level blocks and diagnostics.</summary>
    /// <returns>The recoverable parse result.</returns>
    public VueSingleFileComponentParseResult Parse()
    {
        SingleFileComponentTemplateBlock? template = null;
        SingleFileComponentScriptBlock? script = null;
        SingleFileComponentScriptBlock? scriptSetup = null;
        var styles = new List<SingleFileComponentStyleBlock>();
        var customBlocks = new List<SingleFileComponentCustomBlock>();

        var offset = 0;
        while (offset < source.Length)
        {
            if (char.IsWhiteSpace(source[offset]))
            {
                offset++;
                continue;
            }

            if (StartsWith(offset, "<!--"))
            {
                var commentEnd = source.IndexOf("-->", offset + 4, StringComparison.Ordinal);
                if (commentEnd < 0)
                {
                    Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(offset, source.Length));
                    break;
                }

                offset = commentEnd + 3;
                continue;
            }

            if (source[offset] != '<')
            {
                var strayEnd = source.IndexOf('<', offset);
                if (strayEnd < 0)
                {
                    strayEnd = source.Length;
                }

                Report(SingleFileComponentErrorCode.StrayTopLevelContent, SpanOf(offset, strayEnd));
                offset = strayEnd;
                continue;
            }

            if (StartsWith(offset, "</"))
            {
                var unexpectedEnd = FindTagEnd(offset + 2);
                if (unexpectedEnd < 0)
                {
                    unexpectedEnd = source.Length;
                }

                Report(SingleFileComponentErrorCode.UnexpectedClosingTag, SpanOf(offset, unexpectedEnd));
                offset = unexpectedEnd > offset ? unexpectedEnd : offset + 1;
                continue;
            }

            if (!TryParseOpeningTag(offset, out var header, out var recoveryOffset))
            {
                offset = recoveryOffset > offset ? recoveryOffset : offset + 1;
                continue;
            }

            var contentStart = header.EndOffset;
            int contentEnd;
            int blockEnd;
            if (header.IsSelfClosing)
            {
                contentEnd = contentStart;
                blockEnd = contentStart;
            }
            else
            {
                var isHtmlTemplate = string.Equals(header.Name, "template", StringComparison.Ordinal)
                    && IsHtmlTemplate(header.Options);
                var foundClosingTag = isHtmlTemplate
                    ? TryFindTemplateClosingTag(header.Name, contentStart, out contentEnd, out blockEnd)
                    : TryFindRawClosingTag(header.Name, contentStart, out contentEnd, out blockEnd);

                if (!foundClosingTag)
                {
                    Report(SingleFileComponentErrorCode.UnterminatedTagBlock, header.Location);
                    contentEnd = source.Length;
                    blockEnd = source.Length;
                }
            }

            var block = BuildBlock(header, blockEnd, contentStart, contentEnd);
            AssignBlock(block, ref template, ref script, ref scriptSetup, styles, customBlocks);
            offset = blockEnd;
        }

        var descriptor = new VueSingleFileComponentDescriptor
        {
            Source = source,
            Template = template,
            Script = script,
            ScriptSetup = scriptSetup,
            Styles = new SyntaxList<SingleFileComponentStyleBlock>(styles.ToArray()),
            CustomBlocks = new SyntaxList<SingleFileComponentCustomBlock>(customBlocks.ToArray()),
        };

        return new VueSingleFileComponentParseResult(
            descriptor,
            new SyntaxList<SingleFileComponentError>(errors.ToArray()));
    }

    private bool TryParseOpeningTag(int startOffset, out ParsedTagHeader header, out int recoveryOffset)
    {
        header = default;
        recoveryOffset = startOffset + 1;

        var offset = startOffset + 1;
        if (offset >= source.Length || !IsTagNameStartCharacter(source[offset]))
        {
            recoveryOffset = FindMalformedTagRecovery(startOffset);
            Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(startOffset, recoveryOffset));
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
                Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(startOffset, source.Length));
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
                    SpanOf(startOffset, endOffset));
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
                        SpanOf(startOffset, endOffset));
                    recoveryOffset = endOffset;
                    return true;
                }

                recoveryOffset = FindMalformedTagRecovery(slashOffset);
                Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(slashOffset, recoveryOffset));
                return false;
            }

            if (source[offset] == '<')
            {
                recoveryOffset = offset;
                Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(startOffset, offset));
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
                Report(SingleFileComponentErrorCode.MalformedTagAttribute, SpanOf(optionStart, recoveryOffset));
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
                    Report(SingleFileComponentErrorCode.MalformedTagAttribute, SpanOf(optionStart, recoveryOffset));
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
                        Report(SingleFileComponentErrorCode.MalformedTagAttribute, SpanOf(optionStart, source.Length));
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
                        Report(SingleFileComponentErrorCode.MalformedTagAttribute, SpanOf(optionStart, recoveryOffset));
                        return false;
                    }

                    optionValue = source.Substring(valueStart, offset - valueStart);
                }
            }

            var optionEnd = offset;
            var option = new SingleFileComponentBlockOption(
                optionName,
                optionValue,
                SpanOf(optionStart, optionEnd));
            options.Add(option);

            if (!optionNames.Add(optionName))
            {
                Report(SingleFileComponentErrorCode.DuplicateTagAttribute, option.Location);
            }
        }

        recoveryOffset = source.Length;
        Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(startOffset, source.Length));
        return false;
    }

    private bool TryFindRawClosingTag(string name, int contentStart, out int contentEnd, out int blockEnd)
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

    private bool TryFindTemplateClosingTag(string rootName, int contentStart, out int contentEnd, out int blockEnd)
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

    private SingleFileComponentBlock BuildBlock(
        ParsedTagHeader header,
        int blockEnd,
        int contentStart,
        int contentEnd)
    {
        var content = source.Substring(contentStart, contentEnd - contentStart);
        var options = new SyntaxList<SingleFileComponentBlockOption>(header.Options);
        var blockLocation = SpanOf(header.StartOffset, blockEnd);
        var contentLocation = SpanOf(contentStart, contentEnd);

        return header.Name switch
        {
            "template" => new SingleFileComponentTemplateBlock
            {
                Name = header.Name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            "script" => new SingleFileComponentScriptBlock
            {
                Name = header.Name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            "style" => new SingleFileComponentStyleBlock
            {
                Name = header.Name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            _ => new SingleFileComponentCustomBlock
            {
                Name = header.Name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
        };
    }

    private void AssignBlock(
        SingleFileComponentBlock block,
        ref SingleFileComponentTemplateBlock? template,
        ref SingleFileComponentScriptBlock? script,
        ref SingleFileComponentScriptBlock? scriptSetup,
        List<SingleFileComponentStyleBlock> styles,
        List<SingleFileComponentCustomBlock> customBlocks)
    {
        switch (block)
        {
            case SingleFileComponentTemplateBlock templateBlock:
                if (template is null)
                {
                    template = templateBlock;
                }
                else
                {
                    Report(SingleFileComponentErrorCode.DuplicateTemplateBlock, block.Location);
                }

                break;
            case SingleFileComponentScriptBlock scriptBlock:
                if (scriptBlock.HasOption("setup"))
                {
                    if (scriptSetup is null)
                    {
                        scriptSetup = scriptBlock;
                    }
                    else
                    {
                        Report(SingleFileComponentErrorCode.DuplicateScriptSetupBlock, block.Location);
                    }
                }
                else if (script is null)
                {
                    script = scriptBlock;
                }
                else
                {
                    Report(SingleFileComponentErrorCode.DuplicateScriptBlock, block.Location);
                }

                break;
            case SingleFileComponentStyleBlock styleBlock:
                styles.Add(styleBlock);
                break;
            case SingleFileComponentCustomBlock customBlock:
                customBlocks.Add(customBlock);
                break;
        }
    }

    private static bool IsHtmlTemplate(SingleFileComponentBlockOption[] options)
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

    private int FindTagEnd(int startOffset)
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

    private void SkipWhitespace(ref int offset)
    {
        while (offset < source.Length && char.IsWhiteSpace(source[offset]))
        {
            offset++;
        }
    }

    private bool StartsWith(int offset, string value)
    {
        if (offset < 0 || source.Length - offset < value.Length)
        {
            return false;
        }

        return string.CompareOrdinal(source, offset, value, 0, value.Length) == 0;
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

    private void Report(SingleFileComponentErrorCode code, SourceLocation location)
        => errors.Add(new SingleFileComponentError(code, SingleFileComponentErrorMessages.GetMessage(code), location));

    private SourceLocation SpanOf(int startOffset, int endOffset)
        => new(PositionAt(startOffset), PositionAt(endOffset), source.Substring(startOffset, endOffset - startOffset));

    private Position PositionAt(int offset)
    {
        var low = 0;
        var high = lineStarts.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (lineStarts[middle] <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return new Position(offset, low + 1, (offset - lineStarts[low]) + 1);
    }

    private static int[] BuildLineStarts(string source)
    {
        var starts = new List<int> { 0 };
        var offset = 0;
        while (offset < source.Length)
        {
            if (source[offset] == '\r')
            {
                if (offset + 1 < source.Length && source[offset + 1] == '\n')
                {
                    offset++;
                }

                starts.Add(offset + 1);
            }
            else if (source[offset] == '\n')
            {
                starts.Add(offset + 1);
            }

            offset++;
        }

        return starts.ToArray();
    }

    private readonly struct ParsedTagHeader
    {
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

        public string Name { get; }

        public SingleFileComponentBlockOption[] Options { get; }

        public int StartOffset { get; }

        public int EndOffset { get; }

        public bool IsSelfClosing { get; }

        public SourceLocation Location { get; }
    }
}
