using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The recoverable tag-container scanner behind <see cref="VueSingleFileComponentParser"/>.
/// </summary>
/// <remarks>
/// One engine is used for one source string. Instances are not thread-safe. Root blocks other than an
/// HTML <c>template</c> are scanned as raw text, as the <c>.vue</c> container format requires. Template boundaries
/// are found with a lightweight HTML stack so end-tag text in attributes, comments, and nested raw-text
/// elements cannot close the root template. All tag machinery lives in the shared
/// <see cref="SingleFileComponentTagScanner"/>, which the hybrid <c>.viu</c> engine reuses
/// ([V01.01.06.10]); this engine owns only the top-level dispatch and the <c>.vue</c> descriptor rules.
/// </remarks>
internal sealed class VueSingleFileComponentParseEngine
{
    private readonly string source;
    private readonly int[] lineStarts;
    private readonly List<SingleFileComponentError> errors = new();
    private readonly SingleFileComponentTagScanner scanner;

    /// <summary>Creates a parser engine for one complete <c>.vue</c> source string.</summary>
    /// <param name="source">The source to scan.</param>
    public VueSingleFileComponentParseEngine(string source)
    {
        this.source = source;
        this.lineStarts = BuildLineStarts(source);
        this.scanner = new SingleFileComponentTagScanner(source, SpanOf, Report);
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

            if (scanner.StartsWith(offset, "<!--"))
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

            if (scanner.StartsWith(offset, "</"))
            {
                var unexpectedEnd = scanner.FindTagEnd(offset + 2);
                if (unexpectedEnd < 0)
                {
                    unexpectedEnd = source.Length;
                }

                Report(SingleFileComponentErrorCode.UnexpectedClosingTag, SpanOf(offset, unexpectedEnd));
                offset = unexpectedEnd > offset ? unexpectedEnd : offset + 1;
                continue;
            }

            if (!scanner.TryParseOpeningTag(offset, out var header, out var recoveryOffset))
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
                    && SingleFileComponentTagScanner.IsHtmlTemplate(header.Options);
                var foundClosingTag = isHtmlTemplate
                    ? scanner.TryFindTemplateClosingTag(header.Name, contentStart, out contentEnd, out blockEnd)
                    : scanner.TryFindRawClosingTag(header.Name, contentStart, out contentEnd, out blockEnd);

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

    private SingleFileComponentBlock BuildBlock(
        SingleFileComponentTagScanner.ParsedTagHeader header,
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
}
