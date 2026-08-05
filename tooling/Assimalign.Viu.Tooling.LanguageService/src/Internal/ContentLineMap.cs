using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Maps a block-content offset onto the document's own zero-based line numbering — the single mapping
/// scheme every block-interior feature uses, so template-element folding ([V01.01.12.07.07]) and
/// script-interior folding ([V01.01.12.07.10]) address the same document lines by construction.
/// </summary>
/// <remarks>
/// A block's parse reports offsets relative to the block's <em>content</em>, never to the file. The map
/// carries a line-start table over the content plus the document line the content's first character sits
/// on, taken from the block's <c>ContentLocation</c> — the open-tag line for a tag container, the line
/// after the header for an <c>@</c>-block container. '\n', '\r\n', and a lone '\r' each end one line,
/// matching how the container parse numbers the document's lines.
/// </remarks>
internal sealed class ContentLineMap
{
    private readonly int[] lineStarts;
    private readonly int documentLineOffset;

    private ContentLineMap(int[] lineStarts, int documentLineOffset)
    {
        this.lineStarts = lineStarts;
        this.documentLineOffset = documentLineOffset;
    }

    /// <summary>Creates the map for <paramref name="block"/>'s content.</summary>
    /// <param name="block">The container parse's block.</param>
    /// <returns>The map addressing that block's content in document line coordinates.</returns>
    internal static ContentLineMap Create(SingleFileComponentBlock block)
        => new(
            GetLineStarts(block.Content),
            // Position.Line is one-based, so content line 0 sits on this zero-based document line.
            Math.Max(block.ContentLocation.Start.Line - 1, 0));

    /// <summary>Gets the zero-based document line holding the content character at <paramref name="contentOffset"/>.</summary>
    /// <param name="contentOffset">A zero-based offset into the block content.</param>
    /// <returns>The document line index.</returns>
    internal int GetDocumentLine(int contentOffset)
        => documentLineOffset + GetLineIndex(lineStarts, contentOffset);

    // The start offset of each line in the block content.
    private static int[] GetLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '\r')
            {
                if (index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (character != '\n')
            {
                continue;
            }

            starts.Add(index + 1);
        }

        return starts.ToArray();
    }

    // The index of the line containing offset (binary search over the line starts).
    private static int GetLineIndex(int[] lineStarts, int offset)
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

        return low;
    }
}
