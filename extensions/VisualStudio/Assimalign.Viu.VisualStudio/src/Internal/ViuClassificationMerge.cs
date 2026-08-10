using System;
using System.Collections.Generic;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Overlays exact server classifications on the lexical fallback without producing overlapping
/// editor spans. Only lexical tokens intersecting an exact semantic span are replaced.
/// </summary>
internal static class ViuClassificationMerge
{
    /// <summary>Merges lexical and semantic spans in authored document order.</summary>
    internal static IReadOnlyList<ViuResolvedClassificationSpan> Merge(
        IReadOnlyList<ViuLexicalSpan> lexicalSpans,
        IReadOnlyList<ViuSemanticClassification> semanticClassifications)
    {
        if (lexicalSpans is null)
        {
            throw new ArgumentNullException(nameof(lexicalSpans));
        }

        if (semanticClassifications is null)
        {
            throw new ArgumentNullException(nameof(semanticClassifications));
        }

        var semanticByLine = new Dictionary<int, List<ViuSemanticClassification>>();
        var merged = new List<ViuResolvedClassificationSpan>(
            lexicalSpans.Count + semanticClassifications.Count);
        foreach (var semantic in semanticClassifications)
        {
            if (!semantic.IsValid)
            {
                continue;
            }

            if (!semanticByLine.TryGetValue(semantic.StartLine, out var lineClassifications))
            {
                lineClassifications = [];
                semanticByLine.Add(semantic.StartLine, lineClassifications);
            }

            lineClassifications.Add(semantic);
            merged.Add(
                new ViuResolvedClassificationSpan(
                    semantic.StartLine,
                    semantic.StartCharacter,
                    semantic.EndCharacter - semantic.StartCharacter,
                    semantic.ClassificationTypeName));
        }

        foreach (var lexical in lexicalSpans)
        {
            if (lexical.Length <= 0 ||
                IntersectsSemanticSpan(lexical, semanticByLine))
            {
                continue;
            }

            merged.Add(
                new ViuResolvedClassificationSpan(
                    lexical.LineNumber,
                    lexical.Start,
                    lexical.Length,
                    ViuClassificationTypeNames.GetClassificationTypeName(
                        lexical.ClassificationKind)));
        }

        merged.Sort(
            static (left, right) =>
            {
                var lineComparison = left.LineNumber.CompareTo(right.LineNumber);
                return lineComparison != 0
                    ? lineComparison
                    : left.Start.CompareTo(right.Start);
            });
        return merged;
    }

    private static bool IntersectsSemanticSpan(
        ViuLexicalSpan lexical,
        IReadOnlyDictionary<int, List<ViuSemanticClassification>> semanticByLine)
    {
        if (!semanticByLine.TryGetValue(lexical.LineNumber, out var classifications))
        {
            return false;
        }

        var lexicalEnd = lexical.Start + lexical.Length;
        foreach (var semantic in classifications)
        {
            if (lexical.Start < semantic.EndCharacter &&
                semantic.StartCharacter < lexicalEnd)
            {
                return true;
            }
        }

        return false;
    }
}
