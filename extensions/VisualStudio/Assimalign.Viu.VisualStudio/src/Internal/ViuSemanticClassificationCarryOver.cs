using System;
using System.Collections.Generic;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Carries the last exact classifications forward onto text the server has not answered for yet.
/// </summary>
/// <remarks>
/// <para>
/// A publication describes one exact document text, and the first keystroke after it stops
/// describing what is on screen. Dropping it there is what made a script block change color while
/// being typed in: with no exact answer the lexical fallback is all that is left, and its
/// PascalCase-is-a-type guess repaints every declared name as a class until the next publication
/// lands. Nothing about the document actually changed — only what the editor could still prove.
/// </para>
/// <para>
/// Carrying the spans forward keeps the colors the server did establish, on the words the edit did
/// not touch. A span the edit moved arrives translated; a span the edit consumed is dropped; a span
/// the edit split across lines is dropped, because a classification names a single-line range and
/// half of one is not a smaller truth. The result is provisional by construction and is replaced
/// wholesale by the next publication, so the window it covers is exactly the round trip.
/// </para>
/// <para>
/// Editor-free so the rule is unit-testable: the caller supplies the offset translation its snapshot
/// already knows how to perform, and this decides what survives it.
/// </para>
/// </remarks>
internal static class ViuSemanticClassificationCarryOver
{
    /// <summary>
    /// Translates classifications describing one text onto the text that replaced it.
    /// </summary>
    /// <param name="classifications">The classifications as published, against <paramref name="publishedLineStarts"/>.</param>
    /// <param name="publishedLineStarts">Line start offsets of the text the classifications describe.</param>
    /// <param name="currentLineStarts">Line start offsets of the text they are being carried onto.</param>
    /// <param name="translateOffset">
    /// Maps an offset in the published text to its offset in the current text, or returns a negative
    /// value when the edit consumed that position.
    /// </param>
    /// <returns>The classifications that survived, in the order they were given.</returns>
    public static IReadOnlyList<ViuSemanticClassification> Translate(
        IReadOnlyList<ViuSemanticClassification> classifications,
        IReadOnlyList<int> publishedLineStarts,
        IReadOnlyList<int> currentLineStarts,
        Func<int, int> translateOffset)
    {
        if (classifications is null)
        {
            throw new ArgumentNullException(nameof(classifications));
        }

        if (publishedLineStarts is null)
        {
            throw new ArgumentNullException(nameof(publishedLineStarts));
        }

        if (currentLineStarts is null)
        {
            throw new ArgumentNullException(nameof(currentLineStarts));
        }

        if (translateOffset is null)
        {
            throw new ArgumentNullException(nameof(translateOffset));
        }

        var carried = new List<ViuSemanticClassification>(classifications.Count);
        foreach (ViuSemanticClassification classification in classifications)
        {
            if (!classification.IsValid ||
                classification.StartLine >= publishedLineStarts.Count)
            {
                continue;
            }

            int lineStart = publishedLineStarts[classification.StartLine];
            int translatedStart = translateOffset(lineStart + classification.StartCharacter);
            int translatedEnd = translateOffset(lineStart + classification.EndCharacter);
            if (translatedStart < 0 || translatedEnd <= translatedStart)
            {
                continue;
            }

            int line = FindLine(currentLineStarts, translatedStart);
            if (line < 0 ||
                (line + 1 < currentLineStarts.Count && translatedEnd > currentLineStarts[line + 1]))
            {
                continue;
            }

            carried.Add(new ViuSemanticClassification(
                line,
                translatedStart - currentLineStarts[line],
                line,
                translatedEnd - currentLineStarts[line],
                classification.ClassificationTypeName));
        }

        return carried;
    }

    // The last line whose start is at or before the offset.
    private static int FindLine(IReadOnlyList<int> lineStarts, int offset)
    {
        if (lineStarts.Count == 0 || offset < lineStarts[0])
        {
            return -1;
        }

        int low = 0;
        int high = lineStarts.Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
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
