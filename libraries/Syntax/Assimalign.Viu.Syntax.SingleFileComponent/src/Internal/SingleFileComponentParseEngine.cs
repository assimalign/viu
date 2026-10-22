using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The hybrid-container state machine behind <see cref="SingleFileComponentParser"/>
/// ([V01.01.06.10]). Top-level dispatch is line-oriented and <b>column 0 is structural</b>: a line
/// whose first column is <c>@</c> opens an @-block (<c>@script</c>, custom blocks, and the legacy
/// <c>@template</c>/<c>@style</c> forms), and a line whose first column is <c>&lt;</c> opens a
/// tag-based block (<c>&lt;template&gt;</c>/<c>&lt;style&gt;</c>, the canonical containers). An
/// @-block closes at the first later line whose first column is <c>}</c>; a tag block closes at its
/// matching end tag <em>anywhere</em>, using the shared <see cref="SingleFileComponentTagScanner"/>
/// (the same boundary rules as the <c>.vue</c> engine). The parser only slices — it never looks
/// inside content. See <c>docs/FORMAT.md</c> for the full rule set.
/// </summary>
/// <remarks>Internal and single-use: construct one engine per parse. Not thread-safe.</remarks>
internal sealed class SingleFileComponentParseEngine
{
    private readonly string source;
    private readonly LineSpan[] lines;
    private readonly List<SingleFileComponentError> errors = new();
    private readonly SingleFileComponentTagScanner scanner;

    /// <summary>Creates an engine over <paramref name="source"/>, pre-computing its line table.</summary>
    /// <param name="source">The full <c>.viu</c> file text (never <see langword="null"/>).</param>
    public SingleFileComponentParseEngine(string source)
    {
        this.source = source;
        this.lines = SplitLines(source);
        this.scanner = new SingleFileComponentTagScanner(source, SpanOf, Report);
    }

    /// <summary>Runs the block scan and returns the descriptor plus any recoverable diagnostics.</summary>
    /// <returns>The parse result.</returns>
    public SingleFileComponentParseResult Parse()
    {
        SingleFileComponentTemplateBlock? template = null;
        SingleFileComponentScriptBlock? script = null;
        var styles = new List<SingleFileComponentStyleBlock>();
        var customBlocks = new List<SingleFileComponentCustomBlock>();

        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];

            // Blank lines separate blocks at the top level.
            if (IsBlank(line))
            {
                index++;
                continue;
            }

            // Column 0 is structural. A leading '@' opens an @-block; a leading '<' opens a tag block.
            var first = source[line.Start];
            if (first == '<')
            {
                index = ParseTagAtLineStart(index, out var tagBlock);
                if (tagBlock is not null)
                {
                    AssignBlock(tagBlock, ref template, ref script, styles, customBlocks);
                }

                continue;
            }

            if (first != '@')
            {
                Report(SingleFileComponentErrorCode.StrayTopLevelContent, SpanOf(line.Start, line.TextEnd));
                index++;
                continue;
            }

            if (!TryParseHeader(line, out var header))
            {
                // A diagnostic was already reported; recover by skipping the header line.
                index++;
                continue;
            }

            // The @template/@style containers are legacy since [V01.01.06.10]: they still parse during
            // the migration window, but each header reports a Warning-severity migration diagnostic.
            if (string.Equals(header.Name, "template", StringComparison.Ordinal))
            {
                Report(SingleFileComponentErrorCode.LegacyTemplateBlockSyntax, header.HeaderLocation);
            }
            else if (string.Equals(header.Name, "style", StringComparison.Ordinal))
            {
                Report(SingleFileComponentErrorCode.LegacyStyleBlockSyntax, header.HeaderLocation);
            }

            // The block opened. Its content runs from the next line to the closing '}' at column 0.
            var contentStart = line.End;
            var closingIndex = FindClosingBraceLine(index + 1);

            int contentEnd;
            int blockEndOffset;
            int resumeIndex;
            if (closingIndex >= 0)
            {
                var closing = lines[closingIndex];
                contentEnd = closing.Start;
                blockEndOffset = closing.Start + 1; // the whole block ends just past the '}'
                resumeIndex = closingIndex + 1;
            }
            else
            {
                // Unterminated: recover by taking content to end of file and reporting at the header.
                Report(SingleFileComponentErrorCode.UnterminatedBlock, header.HeaderLocation);
                contentEnd = source.Length;
                blockEndOffset = source.Length;
                resumeIndex = lines.Length;
            }

            var block = BuildBlock(header.Name, header.Options, line.Start, blockEndOffset, contentStart, contentEnd);
            AssignBlock(block, ref template, ref script, styles, customBlocks);
            index = resumeIndex;
        }

        var descriptor = new SingleFileComponentDescriptor
        {
            Source = source,
            Template = template,
            Script = script,
            Styles = new SyntaxList<SingleFileComponentStyleBlock>(styles.ToArray()),
            CustomBlocks = new SyntaxList<SingleFileComponentCustomBlock>(customBlocks.ToArray()),
        };

        return new SingleFileComponentParseResult(descriptor, new SyntaxList<SingleFileComponentError>(errors.ToArray()));
    }

    // Handles a top-level line whose first column is '<': an HTML comment, a canonical
    // <template>/<style> block, the diagnosed-and-discarded <script> tag, a stray closing or unknown
    // tag, or a malformed tag. Returns the line index parsing resumes at; a produced block (template
    // or style only) comes back through the out parameter.
    private int ParseTagAtLineStart(int lineIndex, out SingleFileComponentBlock? block)
    {
        block = null;
        var line = lines[lineIndex];
        var start = line.Start;

        // "<!--" — an HTML comment is tolerated between blocks, matching the .vue container.
        if (scanner.StartsWith(start, "<!--"))
        {
            var commentEnd = source.IndexOf("-->", start + 4, StringComparison.Ordinal);
            if (commentEnd < 0)
            {
                Report(SingleFileComponentErrorCode.MalformedTagBlock, SpanOf(start, source.Length));
                return lines.Length;
            }

            return ResumeAfter(commentEnd + 3);
        }

        // A top-level closing tag has no opening block to close.
        if (scanner.StartsWith(start, "</"))
        {
            var unexpectedEnd = scanner.FindTagEnd(start + 2);
            if (unexpectedEnd < 0)
            {
                unexpectedEnd = source.Length;
            }

            Report(SingleFileComponentErrorCode.UnexpectedClosingTag, SpanOf(start, unexpectedEnd));
            return SkipRestOfLine(unexpectedEnd);
        }

        if (!scanner.TryParseOpeningTag(start, out var header, out var recoveryOffset))
        {
            // The scanner already reported 1009/1010; recover quietly to the next line boundary.
            return SkipRestOfLine(recoveryOffset);
        }

        var name = header.Name;

        // A top-level <script> tag never executes in a .viu file: report it, slice past the element so
        // its content can never reach compilation, and contribute no block.
        if (string.Equals(name, "script", StringComparison.Ordinal))
        {
            Report(SingleFileComponentErrorCode.ScriptTagBlockNotSupported, header.Location);
            if (header.IsSelfClosing)
            {
                return ResumeAfter(header.EndOffset);
            }

            if (scanner.TryFindRawClosingTag(name, header.EndOffset, out _, out var scriptEnd))
            {
                return ResumeAfter(scriptEnd);
            }

            return lines.Length;
        }

        if (string.Equals(name, "template", StringComparison.Ordinal)
            || string.Equals(name, "style", StringComparison.Ordinal))
        {
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
                // An HTML template closes on its nested-markup boundary; a preprocessed template and a
                // style block are raw text to their matching end tag — the same rules as the .vue engine.
                var isHtmlTemplate = string.Equals(name, "template", StringComparison.Ordinal)
                    && SingleFileComponentTagScanner.IsHtmlTemplate(header.Options);
                var foundClosingTag = isHtmlTemplate
                    ? scanner.TryFindTemplateClosingTag(name, contentStart, out contentEnd, out blockEnd)
                    : scanner.TryFindRawClosingTag(name, contentStart, out contentEnd, out blockEnd);

                if (!foundClosingTag)
                {
                    Report(SingleFileComponentErrorCode.UnterminatedTagBlock, header.Location);
                    contentEnd = source.Length;
                    blockEnd = source.Length;
                }
            }

            block = BuildBlock(name, header.Options, header.StartOffset, blockEnd, contentStart, contentEnd);
            return ResumeAfter(blockEnd);
        }

        // Custom blocks stay @-syntax ([V01.01.06.10]): any other top-level tag is stray content.
        // Recovery skips the whole element when its closing tag exists (avoiding per-line diagnostic
        // spam), else moves to the next line.
        Report(SingleFileComponentErrorCode.StrayTopLevelContent, header.Location);
        if (header.IsSelfClosing)
        {
            return SkipRestOfLine(header.EndOffset);
        }

        if (scanner.TryFindRawClosingTag(name, header.EndOffset, out _, out var elementEnd))
        {
            return SkipRestOfLine(elementEnd);
        }

        return lineIndex + 1;
    }

    // Maps the offset just past a completed tag construct to the next top-level line index. A construct
    // that ends mid-line must be followed by only whitespace on its line; a non-whitespace remainder is
    // stray top-level content (blocks open at column 0, so nothing can open there).
    private int ResumeAfter(int offset)
    {
        if (offset >= source.Length)
        {
            return lines.Length;
        }

        var lineIndex = LineIndexAt(offset);
        var line = lines[lineIndex];
        if (offset == line.Start)
        {
            return lineIndex;
        }

        var stray = offset;
        while (stray < line.TextEnd && IsInlineWhitespace(source[stray]))
        {
            stray++;
        }

        if (stray < line.TextEnd)
        {
            Report(SingleFileComponentErrorCode.StrayTopLevelContent, SpanOf(stray, line.TextEnd));
        }

        return lineIndex + 1;
    }

    // Recovery counterpart of ResumeAfter: skips the remainder of the line at offset without further
    // diagnostics (used after a construct that already reported one).
    private int SkipRestOfLine(int offset)
    {
        if (offset >= source.Length)
        {
            return lines.Length;
        }

        var lineIndex = LineIndexAt(offset);
        return offset == lines[lineIndex].Start ? lineIndex : lineIndex + 1;
    }

    // Parses a header line "@<name> [options] {". Returns false (with a diagnostic reported) when the
    // block cannot open — an absent/invalid name, or no opening '{'. Option and trailing-content
    // problems are reported but still open the block, so recovery keeps the sliced content.
    private bool TryParseHeader(LineSpan line, out ParsedHeader header)
    {
        header = default;
        var start = line.Start;
        var end = line.TextEnd;

        // The caller guaranteed source[start] == '@'. The name follows immediately.
        var position = start + 1;
        if (position >= end || !IsNameStartCharacter(source[position]))
        {
            Report(SingleFileComponentErrorCode.MalformedBlockHeader, SpanOf(start, end));
            return false;
        }

        var nameStart = position;
        position++;
        while (position < end && IsNameCharacter(source[position]))
        {
            position++;
        }

        var name = source.Substring(nameStart, position - nameStart);

        var options = new List<SingleFileComponentBlockOption>();
        var headerHadError = false;
        var braceOffset = -1;

        while (position < end)
        {
            var current = source[position];
            if (IsInlineWhitespace(current))
            {
                position++;
                continue;
            }

            if (current == '{')
            {
                braceOffset = position;
                var trailing = position + 1;
                while (trailing < end && IsInlineWhitespace(source[trailing]))
                {
                    trailing++;
                }

                if (trailing < end)
                {
                    Report(SingleFileComponentErrorCode.ContentAfterOpeningBrace, SpanOf(trailing, end));
                }

                break;
            }

            if (IsNameStartCharacter(current))
            {
                var optionStart = position;
                position++;
                while (position < end && IsNameCharacter(source[position]))
                {
                    position++;
                }

                var optionName = source.Substring(optionStart, position - optionStart);
                string? optionValue = null;

                if (position < end && source[position] == '=')
                {
                    position++; // consume '='
                    if (position < end && source[position] == '"')
                    {
                        position++; // consume opening quote
                        var valueStart = position;
                        while (position < end && source[position] != '"')
                        {
                            position++;
                        }

                        if (position < end)
                        {
                            optionValue = source.Substring(valueStart, position - valueStart);
                            position++; // consume closing quote
                        }
                        else
                        {
                            // Unterminated quoted value; recover to end of line.
                            Report(SingleFileComponentErrorCode.MalformedOptionValue, SpanOf(optionStart, end));
                            headerHadError = true;
                        }
                    }
                    else
                    {
                        // Unquoted value; consume the offending token up to whitespace or the brace.
                        while (position < end && !IsInlineWhitespace(source[position]) && source[position] != '{')
                        {
                            position++;
                        }

                        Report(SingleFileComponentErrorCode.MalformedOptionValue, SpanOf(optionStart, position));
                        headerHadError = true;
                    }
                }

                options.Add(new SingleFileComponentBlockOption(optionName, optionValue, SpanOf(optionStart, position)));
                continue;
            }

            // An unexpected character in the header (e.g. '=', '"', '}', or a digit-led token).
            var strayStart = position;
            while (position < end && !IsInlineWhitespace(source[position]) && source[position] != '{')
            {
                position++;
            }

            Report(SingleFileComponentErrorCode.MalformedBlockHeader, SpanOf(strayStart, position));
            headerHadError = true;
        }

        if (braceOffset < 0)
        {
            // No brace to open the block. Suppress a redundant message when we already flagged the header.
            if (!headerHadError)
            {
                Report(SingleFileComponentErrorCode.MissingOpeningBrace, SpanOf(start, end));
            }

            return false;
        }

        header = new ParsedHeader(name, options.ToArray(), SpanOf(start, end));
        return true;
    }

    // Builds the typed block from its name, options, and computed offsets — shared by the @-block and
    // tag-block paths.
    private SingleFileComponentBlock BuildBlock(
        string name,
        SingleFileComponentBlockOption[] headerOptions,
        int blockStart,
        int blockEnd,
        int contentStart,
        int contentEnd)
    {
        var content = source.Substring(contentStart, contentEnd - contentStart);
        var options = new SyntaxList<SingleFileComponentBlockOption>(headerOptions);
        var blockLocation = SpanOf(blockStart, blockEnd);
        var contentLocation = SpanOf(contentStart, contentEnd);

        return name switch
        {
            "template" => new SingleFileComponentTemplateBlock
            {
                Name = name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            "script" => new SingleFileComponentScriptBlock
            {
                Name = name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            "style" => new SingleFileComponentStyleBlock
            {
                Name = name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
            _ => new SingleFileComponentCustomBlock
            {
                Name = name,
                Options = options,
                Content = content,
                Location = blockLocation,
                ContentLocation = contentLocation,
            },
        };
    }

    // Routes a block into the descriptor, enforcing at most one template and one script regardless of
    // which container syntax produced them (a tag <template> and a legacy @template still collide).
    private void AssignBlock(
        SingleFileComponentBlock block,
        ref SingleFileComponentTemplateBlock? template,
        ref SingleFileComponentScriptBlock? script,
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
                if (script is null)
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

    // Finds the first line at or after fromIndex whose first column is '}'.
    private int FindClosingBraceLine(int fromIndex)
    {
        for (var index = fromIndex; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TextEnd > line.Start && source[line.Start] == '}')
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsBlank(LineSpan line)
    {
        for (var offset = line.Start; offset < line.TextEnd; offset++)
        {
            if (!char.IsWhiteSpace(source[offset]))
            {
                return false;
            }
        }

        return true;
    }

    private void Report(SingleFileComponentErrorCode code, SourceLocation location)
        => errors.Add(new SingleFileComponentError(code, SingleFileComponentErrorMessages.GetMessage(code), location));

    private SourceLocation SpanOf(int startOffset, int endOffset)
        => new(PositionAt(startOffset), PositionAt(endOffset), source.Substring(startOffset, endOffset - startOffset));

    // Maps an absolute offset to its (offset, line, column) via the pre-computed line table.
    private Position PositionAt(int offset)
    {
        var line = lines[LineIndexAt(offset)];
        return new Position(offset, line.Number, (offset - line.Start) + 1);
    }

    // Finds the index of the line containing offset (binary search over line starts).
    private int LineIndexAt(int offset)
    {
        var low = 0;
        var high = lines.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (lines[mid].Start <= offset)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    // Splits source into lines, treating \n, \r\n, and a lone \r each as one terminator. A final line
    // (possibly empty) always closes the table so PositionAt resolves the end-of-file offset.
    private static LineSpan[] SplitLines(string source)
    {
        var result = new List<LineSpan>();
        var lineStart = 0;
        var number = 1;
        var index = 0;
        var length = source.Length;

        while (index < length)
        {
            var current = source[index];
            if (current == '\n')
            {
                result.Add(new LineSpan(lineStart, index, index + 1, number));
                index++;
                lineStart = index;
                number++;
            }
            else if (current == '\r')
            {
                if (index + 1 < length && source[index + 1] == '\n')
                {
                    result.Add(new LineSpan(lineStart, index, index + 2, number));
                    index += 2;
                }
                else
                {
                    result.Add(new LineSpan(lineStart, index, index + 1, number));
                    index++;
                }

                lineStart = index;
                number++;
            }
            else
            {
                index++;
            }
        }

        result.Add(new LineSpan(lineStart, length, length, number));
        return result.ToArray();
    }

    private static bool IsNameStartCharacter(char value)
        => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || value == '_';

    private static bool IsNameCharacter(char value)
        => IsNameStartCharacter(value) || (value >= '0' && value <= '9') || value == '-';

    private static bool IsInlineWhitespace(char value)
        => value == ' ' || value == '\t';

    // A parsed @-header: the block name, its options, and the header line's span (for the unterminated
    // and legacy-container diagnostics).
    private readonly struct ParsedHeader
    {
        public ParsedHeader(string name, SingleFileComponentBlockOption[] options, SourceLocation headerLocation)
        {
            Name = name;
            Options = options;
            HeaderLocation = headerLocation;
        }

        public string Name { get; }

        public SingleFileComponentBlockOption[] Options { get; }

        public SourceLocation HeaderLocation { get; }
    }

    // A single source line: [Start, TextEnd) is the visible text, End is the next line's start (past the
    // terminator), and Number is the 1-based line number.
    private readonly struct LineSpan
    {
        public LineSpan(int start, int textEnd, int end, int number)
        {
            Start = start;
            TextEnd = textEnd;
            End = end;
            Number = number;
        }

        public int Start { get; }

        public int TextEnd { get; }

        public int End { get; }

        public int Number { get; }
    }
}
