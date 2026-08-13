using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The affine position map between a single-file component and its emitted generated document
/// ([V01.01.12.23], #259). Built by scanning the emitted text for the simple-form directives
/// <c>#line &lt;n&gt; "&lt;FilePath&gt;"</c> … <c>#line default</c> that
/// <see cref="Assimalign.Viu.Compiler.SingleFileComponent.SingleFileComponentSourceEmitter"/> wraps
/// around the hoisted-using and merged-member regions. Within a region the emitter copies the
/// authored lines verbatim (first line padded to the region's source column), so file line
/// <c>S</c> maps to generated line <c>anchor + 1 + (S − regionStartLine)</c> and columns map
/// identically. A generated span outside a mapped region is suppressed, never misplaced.
/// <para>
/// The render body's span-form <c>#line (…) …</c> directives ([V01.01.05.08]) never take part in that
/// affine map — their target is a compiled expression, not verbatim text — but the mapper does record
/// where they sit. That gives two narrower services over template expressions:
/// <see cref="IsRenderExpressionDiagnostic"/> answers whether a position Roslyn already resolved
/// through a directive truly lands on the authored expression, and
/// <see cref="TryMapTemplateExpressionOffsetToGenerated"/> walks the same alignment forward, so a
/// caret inside an authored expression can be bound where the compiler put it.
/// </para>
/// </summary>
internal sealed class GeneratedScriptDocumentMapper
{
    private readonly string fileText;
    private readonly string generatedText;
    private readonly int[] fileLineStarts;
    private readonly int[] generatedLineStarts;
    private readonly IReadOnlyList<MappedRegion> regions;
    private readonly IReadOnlyDictionary<int, RenderExpressionAnchor> renderExpressionAnchors;

    private GeneratedScriptDocumentMapper(
        string fileText,
        string generatedText,
        int[] fileLineStarts,
        int[] generatedLineStarts,
        IReadOnlyList<MappedRegion> regions,
        IReadOnlyDictionary<int, RenderExpressionAnchor> renderExpressionAnchors)
    {
        this.fileText = fileText;
        this.generatedText = generatedText;
        this.fileLineStarts = fileLineStarts;
        this.generatedLineStarts = generatedLineStarts;
        this.regions = regions;
        this.renderExpressionAnchors = renderExpressionAnchors;
    }

    /// <summary>One simple-form <c>#line</c> region, as zero-based line indexes.</summary>
    /// <param name="FileStartLine">The zero-based file line the region's first content line maps to.</param>
    /// <param name="GeneratedStartLine">The zero-based generated line of the region's first content line.</param>
    /// <param name="LineCount">The number of mapped content lines.</param>
    private readonly record struct MappedRegion(
        int FileStartLine,
        int GeneratedStartLine,
        int LineCount);

    /// <summary>
    /// One span-form <c>#line</c> directive's target, as zero-based positions in the component source.
    /// </summary>
    /// <param name="CharacterOffset">The generated column the directive aligns to <paramref name="StartLine"/>/<paramref name="StartCharacter"/>.</param>
    /// <param name="StartLine">The zero-based file line the mapped expression starts on.</param>
    /// <param name="StartCharacter">The zero-based file column the mapped expression starts at.</param>
    /// <param name="EndLine">The zero-based file line the mapped expression ends on.</param>
    /// <param name="EndCharacter">The zero-based exclusive file column the mapped expression ends at.</param>
    private readonly record struct RenderExpressionAnchor(
        int CharacterOffset,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter);

    /// <summary>
    /// Creates the map for one emitted generated document.
    /// </summary>
    /// <param name="fileText">The single-file component's source text.</param>
    /// <param name="generatedText">The emitted generated document text.</param>
    /// <param name="filePath">
    /// The component file path exactly as it was fed to the emitter — the path each simple-form
    /// directive names verbatim.
    /// </param>
    /// <returns>The mapper.</returns>
    internal static GeneratedScriptDocumentMapper Create(
        string fileText,
        string generatedText,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        ArgumentNullException.ThrowIfNull(generatedText);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileLineStarts = ComputeLineStarts(fileText);
        var generatedLineStarts = ComputeLineStarts(generatedText);
        var regions = ScanRegions(
            generatedText,
            generatedLineStarts,
            filePath,
            fileLineStarts.Length,
            out var renderExpressionAnchors);
        return new GeneratedScriptDocumentMapper(
            fileText,
            generatedText,
            fileLineStarts,
            generatedLineStarts,
            regions,
            renderExpressionAnchors);
    }

    /// <summary>
    /// Maps a file offset into the generated document. Fails — rather than answering with a wrong
    /// position — when the offset's line is outside every mapped region or its column overruns the
    /// mapped generated line.
    /// </summary>
    /// <param name="fileOffset">The zero-based offset within the component source text.</param>
    /// <param name="generatedOffset">The zero-based offset within the generated document.</param>
    /// <returns>Whether the offset mapped.</returns>
    internal bool TryMapFileOffsetToGenerated(int fileOffset, out int generatedOffset)
    {
        generatedOffset = 0;
        if (fileOffset < 0 || fileOffset > fileText.Length)
        {
            return false;
        }

        var fileLine = GetLineIndex(fileLineStarts, fileOffset);
        var column = fileOffset - fileLineStarts[fileLine];
        foreach (var region in regions)
        {
            if (fileLine < region.FileStartLine ||
                fileLine >= region.FileStartLine + region.LineCount)
            {
                continue;
            }

            var generatedLine = region.GeneratedStartLine + (fileLine - region.FileStartLine);
            var lineStart = generatedLineStarts[generatedLine];
            var lineEnd = GetLineEnd(generatedText, generatedLineStarts, generatedLine);
            if (lineStart + column > lineEnd)
            {
                return false;
            }

            generatedOffset = lineStart + column;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Maps a generated-document span back onto the component source — the inverse of
    /// <see cref="TryMapFileOffsetToGenerated"/>. A span outside the mapped regions (the scaffold,
    /// the span-form-mapped render body, or a span crossing a region edge) fails, and the mapped
    /// text is verified character-for-character so first-line padding can never smuggle a span onto
    /// the wrong file characters: unmapped spans are suppressed, never misplaced.
    /// </summary>
    /// <param name="generatedStart">The zero-based start offset within the generated document.</param>
    /// <param name="generatedLength">The span length.</param>
    /// <param name="fileStart">The zero-based start offset within the component source.</param>
    /// <param name="fileLength">The mapped span length (equal to <paramref name="generatedLength"/>).</param>
    /// <returns>Whether the span mapped.</returns>
    internal bool TryMapGeneratedSpanToFile(
        int generatedStart,
        int generatedLength,
        out int fileStart,
        out int fileLength)
    {
        fileStart = 0;
        fileLength = 0;
        if (generatedStart < 0 ||
            generatedLength < 0 ||
            generatedStart + generatedLength > generatedText.Length)
        {
            return false;
        }

        var generatedEnd = generatedStart + generatedLength;
        var startLine = GetLineIndex(generatedLineStarts, generatedStart);
        var endLine = GetLineIndex(generatedLineStarts, generatedEnd);
        foreach (var region in regions)
        {
            if (startLine < region.GeneratedStartLine ||
                startLine >= region.GeneratedStartLine + region.LineCount)
            {
                continue;
            }

            if (endLine >= region.GeneratedStartLine + region.LineCount)
            {
                return false;
            }

            var fileStartLine = region.FileStartLine + (startLine - region.GeneratedStartLine);
            var fileEndLine = region.FileStartLine + (endLine - region.GeneratedStartLine);
            var mappedStart = fileLineStarts[fileStartLine] +
                (generatedStart - generatedLineStarts[startLine]);
            var mappedEnd = fileLineStarts[fileEndLine] +
                (generatedEnd - generatedLineStarts[endLine]);
            if (mappedStart < 0 ||
                mappedEnd < mappedStart ||
                mappedEnd > fileText.Length)
            {
                return false;
            }

            // The verbatim-copy guarantee makes mapped text identical by construction; anything
            // else (a span reaching into first-line padding, a pathological directive inside
            // authored content) must be suppressed rather than reported on the wrong characters.
            if (string.CompareOrdinal(
                    generatedText,
                    generatedStart,
                    fileText,
                    mappedStart,
                    generatedLength) != 0)
            {
                return false;
            }

            fileStart = mappedStart;
            fileLength = mappedEnd - mappedStart;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a compiler diagnostic that Roslyn resolved through a render-body span
    /// directive genuinely lands on the template expression that directive names.
    /// </summary>
    /// <param name="generatedStartLine">The zero-based generated line the diagnostic starts on.</param>
    /// <param name="generatedStartCharacter">The zero-based generated column the diagnostic starts at.</param>
    /// <param name="mappedStartLine">The zero-based file line Roslyn mapped the diagnostic's start to.</param>
    /// <param name="mappedStartCharacter">The zero-based file column Roslyn mapped the diagnostic's start to.</param>
    /// <param name="mappedEndLine">The zero-based file line Roslyn mapped the diagnostic's end to.</param>
    /// <param name="mappedEndCharacter">The zero-based file column Roslyn mapped the diagnostic's end to.</param>
    /// <returns>Whether the diagnostic may be reported at the mapped position.</returns>
    /// <remarks>
    /// A span directive's mapping stays in effect for its whole generated line, and that line also
    /// carries the scaffolding the code generator wrapped the expression in. Two bounds separate the
    /// two: the diagnostic must begin at or after the column the directive anchors (everything to its
    /// left is scaffolding, which Roslyn would clamp onto the expression's first character), and its
    /// mapped span must fall inside the expression span the directive names (everything to its right
    /// extrapolates past the expression's last character). A generator error therefore stays a
    /// generator concern instead of being reported on code the author wrote.
    /// </remarks>
    internal bool IsRenderExpressionDiagnostic(
        int generatedStartLine,
        int generatedStartCharacter,
        int mappedStartLine,
        int mappedStartCharacter,
        int mappedEndLine,
        int mappedEndCharacter)
    {
        if (!renderExpressionAnchors.TryGetValue(generatedStartLine, out var anchor) ||
            generatedStartCharacter < anchor.CharacterOffset)
        {
            return false;
        }

        return !IsBefore(
                   mappedStartLine,
                   mappedStartCharacter,
                   anchor.StartLine,
                   anchor.StartCharacter) &&
               !IsBefore(
                   anchor.EndLine,
                   anchor.EndCharacter,
                   mappedEndLine,
                   mappedEndCharacter);
    }

    private static bool IsBefore(int line, int character, int otherLine, int otherCharacter)
        => line < otherLine || (line == otherLine && character < otherCharacter);

    /// <summary>
    /// Maps an offset inside an authored template expression onto the position the compiler emitted
    /// it at, so the expression can be bound where its meaning actually lives — inside the render
    /// body. Fails, rather than answering with a wrong position, for any offset the render source map
    /// does not cover.
    /// </summary>
    /// <param name="fileOffset">The zero-based offset within the component source text.</param>
    /// <param name="generatedOffset">The zero-based offset within the generated document.</param>
    /// <returns>Whether the offset mapped.</returns>
    /// <remarks>
    /// A span directive aligns its generated anchor column to the expression's first character and
    /// runs forward from there character by character, which is exactly what makes the inverse
    /// available: the offset's distance from the expression's start is its distance from the anchor
    /// column. The compiler rewrites expression <em>roots</em> (a component member becomes
    /// <c>component.Member</c>) but records the mapping at the rewritten identifier, so the text from
    /// the anchor onward is the authored text verbatim - verified here character-for-character before
    /// answering, so a rewrite the map did not anticipate can never place the caret on another token.
    /// <para>
    /// An anchor says where an expression <em>starts</em>, not how far it reaches: a rewritten root is
    /// recorded against the identifier alone, so the rest of <c>Item.Name</c> lies past the anchor
    /// while still being the same expression running verbatim. The nearest anchor beginning at or
    /// before the offset on its line therefore wins, and the character-for-character check is what
    /// makes reaching past it safe. An offset the compiler dropped entirely - a malformed
    /// <c>v-for</c>, an expression sharing a generated line with the one that claimed the anchor -
    /// has no image at all.
    /// </para>
    /// </remarks>
    internal bool TryMapTemplateExpressionOffsetToGenerated(int fileOffset, out int generatedOffset)
    {
        generatedOffset = 0;
        if (fileOffset < 0 || fileOffset > fileText.Length)
        {
            return false;
        }

        var fileLine = GetLineIndex(fileLineStarts, fileOffset);
        var matchedStart = -1;
        foreach (var pair in renderExpressionAnchors)
        {
            var anchor = pair.Value;
            if (pair.Key >= generatedLineStarts.Length ||
                anchor.StartLine != fileLine ||
                anchor.StartLine >= fileLineStarts.Length)
            {
                continue;
            }

            var expressionStart = fileLineStarts[anchor.StartLine] + anchor.StartCharacter;
            var offsetInExpression = fileOffset - expressionStart;
            if (offsetInExpression < 0 || expressionStart <= matchedStart)
            {
                continue;
            }

            var anchorOffset = generatedLineStarts[pair.Key] + anchor.CharacterOffset;
            var candidate = anchorOffset + offsetInExpression;
            if (candidate > GetLineEnd(generatedText, generatedLineStarts, pair.Key) ||
                string.CompareOrdinal(
                    generatedText,
                    anchorOffset,
                    fileText,
                    expressionStart,
                    offsetInExpression) != 0)
            {
                continue;
            }

            generatedOffset = candidate;
            matchedStart = expressionStart;
        }

        return matchedStart >= 0;
    }

    private static IReadOnlyList<MappedRegion> ScanRegions(
        string generatedText,
        int[] generatedLineStarts,
        string filePath,
        int fileLineCount,
        out IReadOnlyDictionary<int, RenderExpressionAnchor> renderExpressionAnchors)
    {
        var regions = new List<MappedRegion>();
        var anchors = new Dictionary<int, RenderExpressionAnchor>();
        renderExpressionAnchors = anchors;
        var expectedSuffix = " \"" + filePath + "\"";
        var pendingFileStartLine = -1;
        var pendingGeneratedStartLine = 0;
        for (var line = 0; line < generatedLineStarts.Length; line++)
        {
            var text = GetLineText(generatedText, generatedLineStarts, line);
            if (!text.StartsWith("#line", StringComparison.Ordinal))
            {
                continue;
            }

            // Every directive — #line default, the render body's span form, another simple
            // directive — ends the pending region at the previous line.
            if (pendingFileStartLine >= 0)
            {
                var lineCount = line - pendingGeneratedStartLine;
                if (lineCount > 0)
                {
                    regions.Add(new MappedRegion(
                        pendingFileStartLine,
                        pendingGeneratedStartLine,
                        lineCount));
                }

                pendingFileStartLine = -1;
            }

            if (TryParseSimpleDirective(text, expectedSuffix, out var fileStartLine) &&
                fileStartLine - 1 < fileLineCount)
            {
                pendingFileStartLine = fileStartLine - 1;
                pendingGeneratedStartLine = line + 1;
            }
            else if (TryParseSpanDirective(text, expectedSuffix, out var anchor) &&
                     anchor.EndLine < fileLineCount)
            {
                // The emitter brackets exactly one generated line per span directive, closing it with
                // #line default, so the anchor governs the single line that follows.
                anchors[line + 1] = anchor;
            }
        }

        // The emitter always closes a region with #line default; an unterminated tail region is
        // tolerated defensively rather than dropped.
        if (pendingFileStartLine >= 0)
        {
            var lineCount = generatedLineStarts.Length - pendingGeneratedStartLine;
            if (lineCount > 0)
            {
                regions.Add(new MappedRegion(
                    pendingFileStartLine,
                    pendingGeneratedStartLine,
                    lineCount));
            }
        }

        return regions;
    }

    // Matches only the simple form `#line <digits> "<filePath>"`. The render body's span form
    // (`#line (…) … "<filePath>"`) shares the suffix but its middle never parses as a bare
    // integer, which is the deliberate discriminator.
    private static bool TryParseSimpleDirective(
        string lineText,
        string expectedSuffix,
        out int fileStartLine)
    {
        fileStartLine = 0;
        const string Prefix = "#line ";
        if (!lineText.StartsWith(Prefix, StringComparison.Ordinal) ||
            !lineText.EndsWith(expectedSuffix, StringComparison.Ordinal) ||
            lineText.Length <= Prefix.Length + expectedSuffix.Length)
        {
            return false;
        }

        var numberText = lineText.Substring(
            Prefix.Length,
            lineText.Length - Prefix.Length - expectedSuffix.Length);
        return int.TryParse(
                   numberText,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out fileStartLine) &&
               fileStartLine > 0;
    }

    // Matches only the span form `#line (startLine,startColumn)-(endLine,endColumn) charOffset "path"`
    // the render-body source mapper emits. Positions in the directive are one-based per the #line
    // convention and are stored zero-based.
    private static bool TryParseSpanDirective(
        string lineText,
        string expectedSuffix,
        out RenderExpressionAnchor anchor)
    {
        anchor = default;
        const string Prefix = "#line (";
        if (!lineText.StartsWith(Prefix, StringComparison.Ordinal) ||
            !lineText.EndsWith(expectedSuffix, StringComparison.Ordinal) ||
            lineText.Length <= Prefix.Length + expectedSuffix.Length)
        {
            return false;
        }

        var body = lineText.Substring(
            Prefix.Length,
            lineText.Length - Prefix.Length - expectedSuffix.Length);
        var separator = body.IndexOf(")-(", StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        var remainder = body.Substring(separator + 3);
        var close = remainder.IndexOf(')');
        if (close < 0 ||
            !TryParsePosition(body.Substring(0, separator), out var startLine, out var startCharacter) ||
            !TryParsePosition(remainder.Substring(0, close), out var endLine, out var endCharacter) ||
            !TryParseNumber(remainder.Substring(close + 1).Trim(), out var characterOffset))
        {
            return false;
        }

        anchor = new RenderExpressionAnchor(
            characterOffset,
            startLine - 1,
            startCharacter - 1,
            endLine - 1,
            endCharacter - 1);
        return true;
    }

    private static bool TryParsePosition(string text, out int line, out int character)
    {
        line = 0;
        character = 0;
        var separator = text.IndexOf(',');
        return separator >= 0 &&
               TryParseNumber(text.Substring(0, separator), out line) &&
               TryParseNumber(text.Substring(separator + 1), out character) &&
               line > 0 &&
               character > 0;
    }

    private static bool TryParseNumber(string text, out int value)
        => int.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value);

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToArray();
    }

    // The last line whose start is at or before the offset.
    private static int GetLineIndex(int[] lineStarts, int offset)
    {
        var low = 0;
        var high = lineStarts.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (lineStarts[middle] <= offset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    // The offset of the line's terminating '\n', or the text length for the final line.
    private static int GetLineEnd(string text, int[] lineStarts, int line)
        => line + 1 < lineStarts.Length ? lineStarts[line + 1] - 1 : text.Length;

    private static string GetLineText(string text, int[] lineStarts, int line)
    {
        var start = lineStarts[line];
        return text.Substring(start, GetLineEnd(text, lineStarts, line) - start);
    }
}
