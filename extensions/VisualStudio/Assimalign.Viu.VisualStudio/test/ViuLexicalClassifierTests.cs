using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Extensibility.Editor;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLexicalClassifierTests
{
    [Fact]
    public void Classify_AllViuSections_ProducesSectionSpecificClassifications()
    {
        string[] lines =
        [
            "@template {",
            "    <button type=\"button\" @click=\"Increment\">{{ Count }}</button>",
            "}",
            "@script {",
            "    public Reference<int> Count { get; } = Reactive.Reference(0);",
            "}",
            "@style scoped {",
            "    button { color: red; }",
            "}",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttributeValue);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Type);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Method);
        ClassificationsOnLine(spans, 7).ShouldContain(ViuClassificationKind.MarkupAttribute);
    }

    [Fact]
    public void Classify_CommentsAndStrings_DoesNotTreatCommentTokensInsideStringsAsComments()
    {
        string[] lines =
        [
            "@script {",
            "    string address = \"https://example.test/path\";",
            "    // actual comment",
            "}",
            "@style {",
            "    .icon { background: url(\"data:image/svg+xml;/*value*/\"); }",
            "    /* actual style comment */",
            "}",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.String);
        ClassificationsOnLine(spans, 1).ShouldNotContain(ViuClassificationKind.Comment);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.Comment);
        ClassificationsOnLine(spans, 5).ShouldContain(ViuClassificationKind.String);
        ClassificationsOnLine(spans, 5).ShouldNotContain(ViuClassificationKind.Comment);
        ClassificationsOnLine(spans, 6).ShouldContain(ViuClassificationKind.Comment);
    }

    [Fact]
    public void GetClassificationType_Method_UsesVisualStudioSupportedIdentifierClassification()
    {
        ClassificationType classificationType =
            ViuClassificationTagger.GetClassificationType(ViuClassificationKind.Method);

        classificationType.ShouldBe(ClassificationType.KnownValues.Identifier);
    }

    [Fact]
    public void GetClassificationType_Punctuation_UsesBaseEditorOperatorClassification()
    {
        ClassificationType classificationType =
            ViuClassificationTagger.GetClassificationType(ViuClassificationKind.Punctuation);

        classificationType.ShouldBe(ClassificationType.KnownValues.Operator);
    }

    private static ViuClassificationKind[] ClassificationsOnLine(
        IReadOnlyList<ViuLexicalSpan> spans,
        int lineNumber)
    {
        return spans
            .Where(span => span.LineNumber == lineNumber)
            .Select(span => span.ClassificationKind)
            .ToArray();
    }
}
