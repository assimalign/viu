using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Extensibility.Editor;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLexicalClassifierTests
{
    [Fact]
    public void Classify_LegacyAtBlockSections_ProducesSectionSpecificClassifications()
    {
        // Transition-window pin ([V01.01.06.10]): the legacy @template/@style containers keep
        // highlighting until they are removed; @script remains the canonical C# container.
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
    public void Classify_HybridContainerSections_ProducesSectionSpecificClassifications()
    {
        // The canonical hybrid .viu container ([V01.01.06.10]): tag-delimited <template>/<style>
        // blocks plus the @script @-block.
        string[] lines =
        [
            "<template>",
            "    <button type=\"button\" @click=\"Increment\">{{ Count }}</button>",
            "</template>",
            "@script {",
            "    public Reference<int> Count { get; } = Reactive.Reference(0);",
            "}",
            "<style scoped>",
            "    button { color: red; }",
            "</style>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttributeValue);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Directive);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 3).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Type);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Method);
        ClassificationsOnLine(spans, 6).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 6).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 7).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 8).ShouldContain(ViuClassificationKind.MarkupNode);
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
            "<style>",
            "    .icon { background: url(\"data:image/svg+xml;/*value*/\"); }",
            "    /* actual style comment */",
            "</style>",
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
    public void Classify_PascalCaseTemplateTag_ClassifiesAsComponent()
    {
        // Vue names components in PascalCase; plain lowercase names are HTML elements or the
        // lowercase Vue built-ins: https://vuejs.org/guide/components/registration
        string[] lines =
        [
            "<template>",
            "    <RouterView></RouterView>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Component);
        ClassificationsOnLine(spans, 1).ShouldNotContain(ViuClassificationKind.MarkupNode);
    }

    [Fact]
    public void Classify_LowercaseTemplateTag_ClassifiesAsMarkupNode()
    {
        string[] lines =
        [
            "<template>",
            "    <div><slot /></div>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldNotContain(ViuClassificationKind.Component);
    }

    [Fact]
    public void Classify_DirectiveAttributes_ClassifiesAllSigilsAsDirective()
    {
        string[] lines =
        [
            "<template>",
            "    <div v-if=\"Visible\" :value=\"Count\" @click=\"Increment\" #header v-else></div>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        string[] directiveTexts = SpanTexts(spans, lines, 1, ViuClassificationKind.Directive);
        directiveTexts.ShouldContain("v-if");
        directiveTexts.ShouldContain(":value");
        directiveTexts.ShouldContain("@click");
        directiveTexts.ShouldContain("#header");
        directiveTexts.ShouldContain("v-else");
    }

    [Fact]
    public void Classify_InterpolationContent_ClassifiesScriptTokens()
    {
        string[] lines =
        [
            "<template>",
            "    <p>{{ Count + 1 }}</p>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ViuClassificationKind[] classifications = ClassificationsOnLine(spans, 1);
        classifications.ShouldContain(ViuClassificationKind.InterpolationDelimiter);
        classifications.ShouldContain(ViuClassificationKind.Type);
        classifications.ShouldContain(ViuClassificationKind.Operator);
        classifications.ShouldContain(ViuClassificationKind.Number);
    }

    [Fact]
    public void Classify_ClassAttribute_SplitsUtilityVariantsAndClasses()
    {
        string[] lines =
        [
            "<template>",
            "    <div class=\"flex hover:bg-red-500 md:p-[3px]\"></div>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.UtilityVariant)
            .ShouldBe(["hover:", "md:"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.UtilityClass)
            .ShouldBe(["flex", "bg-red-500", "p-[3px]"]);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttributeValue);
    }

    [Fact]
    public void Classify_HybridTemplateTags_TracksTagDelimitedSection()
    {
        // Nested <template #header> slot fragments must not end the tag-delimited section; content
        // after the true closer is top-level text the template rules must not color.
        string[] lines =
        [
            "<template>",
            "    <template #header>",
            "        <span>Title</span>",
            "    </template>",
            "</template> trailing-text",
            "stray top-level text",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Directive);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.MarkupNode);
        spans.Where(span => span.LineNumber == 4)
            .ShouldAllBe(span => span.Start < "</template>".Length);
        ClassificationsOnLine(spans, 5).ShouldBeEmpty();
    }

    [Fact]
    public void Classify_HybridStyleTags_TracksTagDelimitedSection()
    {
        string[] lines =
        [
            "<style scoped>",
            "    --brand-color: #123456;",
            "    button { color: red; }",
            "</style>",
            "stray top-level text",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.MarkupAttribute);
        // Custom properties borrow the type category so theme tokens stand apart from ordinary
        // declarations.
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Type);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 3).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 4).ShouldBeEmpty();
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

    [Fact]
    public void GetClassificationType_Component_UsesTypeClassification()
    {
        ClassificationType classificationType =
            ViuClassificationTagger.GetClassificationType(ViuClassificationKind.Component);

        classificationType.ShouldBe(ClassificationType.KnownValues.Type);
    }

    [Fact]
    public void GetClassificationType_Directive_UsesKeywordClassification()
    {
        ClassificationType classificationType =
            ViuClassificationTagger.GetClassificationType(ViuClassificationKind.Directive);

        classificationType.ShouldBe(ClassificationType.KnownValues.Keyword);
    }

    [Fact]
    public void GetClassificationType_UtilityClass_UsesStringClassification()
    {
        ClassificationType classificationType =
            ViuClassificationTagger.GetClassificationType(ViuClassificationKind.UtilityClass);

        classificationType.ShouldBe(ClassificationType.KnownValues.String);
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

    private static string[] SpanTexts(
        IReadOnlyList<ViuLexicalSpan> spans,
        IReadOnlyList<string> lines,
        int lineNumber,
        ViuClassificationKind classificationKind)
    {
        return spans
            .Where(span => span.LineNumber == lineNumber && span.ClassificationKind == classificationKind)
            .OrderBy(span => span.Start)
            .Select(span => lines[lineNumber].Substring(span.Start, span.Length))
            .ToArray();
    }
}
