using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

using Assimalign.Viu.VisualStudio;

namespace Assimalign.Viu.VisualStudio.Tests;

/// <summary>
/// Pins the non-overlapping merge between lexical fallback spans and exact server-authored C#
/// classifications. [V01.01.12.07.11]
/// </summary>
public class ViuClassificationMergeTests
{
    [Fact]
    public void Merge_SemanticIdentifiers_ReplaceOnlyIntersectingLexicalSpans()
    {
        IReadOnlyList<ViuLexicalSpan> lexical =
        [
            new ViuLexicalSpan(0, 0, 6, ViuClassificationKind.Keyword),
            new ViuLexicalSpan(1, 4, 5, ViuClassificationKind.Type),
            new ViuLexicalSpan(1, 12, 5, ViuClassificationKind.Identifier),
        ];
        IReadOnlyList<ViuSemanticClassification> semantic =
        [
            new ViuSemanticClassification(1, 4, 1, 9, "property name"),
            new ViuSemanticClassification(1, 12, 1, 17, "local name"),
        ];

        IReadOnlyList<ViuResolvedClassificationSpan> merged =
            ViuClassificationMerge.Merge(lexical, semantic);

        merged.Count.ShouldBe(3);
        merged[0].ShouldBe(new ViuResolvedClassificationSpan(0, 0, 6, "keyword"));
        merged[1].ShouldBe(new ViuResolvedClassificationSpan(1, 4, 5, "property name"));
        merged[2].ShouldBe(new ViuResolvedClassificationSpan(1, 12, 5, "local name"));
        merged.Count(span => span.LineNumber == 1 && span.Start == 4).ShouldBe(1);
        merged.Count(span => span.LineNumber == 1 && span.Start == 12).ShouldBe(1);
    }

    [Fact]
    public void Merge_NoSemanticSnapshot_ReturnsEveryLexicalFallbackSpan()
    {
        IReadOnlyList<ViuLexicalSpan> lexical =
        [
            new ViuLexicalSpan(2, 4, 5, ViuClassificationKind.Type),
            new ViuLexicalSpan(2, 10, 4, ViuClassificationKind.Method),
        ];

        IReadOnlyList<ViuResolvedClassificationSpan> merged =
            ViuClassificationMerge.Merge(lexical, []);

        merged.ShouldBe(
        [
            new ViuResolvedClassificationSpan(2, 4, 5, "class name"),
            new ViuResolvedClassificationSpan(2, 10, 4, "method name"),
        ]);
    }
}
