using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// The affine position map between a single-file component and its emitted generated document
/// ([V01.01.12.23], #259). Built by scanning the emitted text for the simple-form directives
/// <c>#line &lt;n&gt; "&lt;FilePath&gt;"</c> … <c>#line default</c> that
/// <see cref="Assimalign.Viu.Tooling.SingleFileComponent.SingleFileComponentSourceEmitter"/> wraps
/// around the hoisted-using and merged-member regions. Within a region the emitter copies the
/// authored lines verbatim (first line padded to the region's source column), so file line
/// <c>S</c> maps to generated line <c>anchor + 1 + (S − regionStartLine)</c> and columns map
/// identically. The render body's span-form <c>#line (…) …</c> directives are deliberately not
/// matched: a generated span outside a mapped region is suppressed, never misplaced.
/// </summary>
internal sealed class GeneratedScriptDocumentMapper
{
    private readonly string fileText;
    private readonly string generatedText;
    private readonly int[] fileLineStarts;
    private readonly int[] generatedLineStarts;
    private readonly IReadOnlyList<MappedRegion> regions;

    private GeneratedScriptDocumentMapper(
        string fileText,
        string generatedText,
        int[] fileLineStarts,
        int[] generatedLineStarts,
        IReadOnlyList<MappedRegion> regions)
    {
        this.fileText = fileText;
        this.generatedText = generatedText;
        this.fileLineStarts = fileLineStarts;
        this.generatedLineStarts = generatedLineStarts;
        this.regions = regions;
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
            fileLineStarts.Length);
        return new GeneratedScriptDocumentMapper(
            fileText,
            generatedText,
            fileLineStarts,
            generatedLineStarts,
            regions);
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

    private static IReadOnlyList<MappedRegion> ScanRegions(
        string generatedText,
        int[] generatedLineStarts,
        string filePath,
        int fileLineCount)
    {
        var regions = new List<MappedRegion>();
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
