using System.Collections.Generic;
using System.Linq;

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

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttributeValue);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Type);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Method);
        ClassificationsOnLine(spans, 7).ShouldContain(ViuClassificationKind.MarkupAttribute);
    }

    [Fact]
    public void Classify_LegacyScriptHeader_KeepsTheKeywordClassification()
    {
        // The @script header introduces C#, so it stays in the C# keyword color rather than joining
        // the framework-tag palette its @template/@style siblings use for their containers.
        string[] lines =
        [
            "@script {",
            "}",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 0, ViuClassificationKind.Keyword).ShouldBe(["@script"]);
        ClassificationsOnLine(spans, 0).ShouldNotContain(ViuClassificationKind.FrameworkTag);
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

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupAttributeValue);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Directive);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 3).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Keyword);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Type);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.Method);
        ClassificationsOnLine(spans, 6).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 6).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 7).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 8).ShouldContain(ViuClassificationKind.FrameworkTag);
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
    public void Classify_TagNames_SplitFrameworkTagsElementsAndComponents()
    {
        // Three ownership classes, decided lexically. template/slot/style/script are Viu's own tags
        // wherever they appear; a PascalCase or dotted name is a component ([CMP-6]); everything else
        // is an HTML element.
        string[] lines =
        [
            "<template>",
            "    <div><slot /></div>",
            "    <RouterView></RouterView>",
            "    <Layout.Header />",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.MarkupNode).ShouldBe(["div", "div"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.FrameworkTag).ShouldBe(["slot"]);
        SpanTexts(spans, lines, 2, ViuClassificationKind.Component)
            .ShouldBe(["RouterView", "RouterView"]);
        SpanTexts(spans, lines, 3, ViuClassificationKind.Component).ShouldBe(["Layout.Header"]);
        SpanTexts(spans, lines, 0, ViuClassificationKind.FrameworkTag).ShouldBe(["template"]);
        SpanTexts(spans, lines, 4, ViuClassificationKind.FrameworkTag).ShouldBe(["template"]);
    }

    [Fact]
    public void Classify_TagPunctuation_ClassifiesAsDelimiterRatherThanOperator()
    {
        // Tag punctuation carries its own muted classification so structure recedes behind names;
        // '=' between an attribute and its value belongs to the same family.
        string[] lines =
        [
            "<template>",
            "    <div id=\"root\" />",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Delimiter).ShouldBe(["<", "=", "/", ">"]);
        ClassificationsOnLine(spans, 1).ShouldNotContain(ViuClassificationKind.Operator);
        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.Delimiter);
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
    public void Classify_EventHandlerBinding_ClassifiesTheBareIdentifierAsMethod()
    {
        // The handler slot of an event binding is a method position, with or without parentheses.
        string[] lines =
        [
            "<template>",
            "    <button @click=\"Increment\" v-on:input=\"Handle\"></button>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Method)
            .ShouldBe(["Increment", "Handle"]);
    }

    [Fact]
    public void Classify_EventHandlerBindingWithReceiver_LeavesTheReceiverToTheTypePass()
    {
        string[] lines =
        [
            "<template>",
            "    <button @click=\"ViewModel.Increment\"></button>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Method).ShouldBe(["Increment"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Type).ShouldBe(["ViewModel"]);
    }

    [Fact]
    public void Classify_PlainBinding_KeepsIdentifiersAsIdentifiers()
    {
        // A plain binding names component state, not a method and not a type. Call syntax is still a
        // method position anywhere a C# pass runs, so Format(...) stays a method here.
        string[] lines =
        [
            "<template>",
            "    <p :value=\"Count\" v-if=\"Visible\" :text=\"Format(Count)\"></p>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        string[] identifierTexts = SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier);
        identifierTexts.ShouldContain("Count");
        identifierTexts.ShouldContain("Visible");
        SpanTexts(spans, lines, 1, ViuClassificationKind.Method).ShouldBe(["Format"]);
    }

    [Fact]
    public void Classify_BindingExpressionInterior_RunsTheCSharpTokenPasses()
    {
        // A binding value is C# source: its strings, numbers, keywords, and operators color as C#,
        // and only the quotes stay part of the attribute value.
        string[] lines =
        [
            "<template>",
            "    <p :text='Count > 2 ? \"more\" : null'></p>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Number).ShouldBe(["2"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.String).ShouldBe(["\"more\""]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Keyword).ShouldBe(["null"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier).ShouldBe(["Count"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.MarkupAttributeValue)
            .ShouldBe(["'", "'"]);
    }

    [Fact]
    public void Classify_InterpolationContent_ClassifiesScriptTokens()
    {
        // An interpolation hole names component state exactly as a binding value does, so Count is a
        // member rather than the type the PascalCase heuristic would otherwise make of it.
        string[] lines =
        [
            "<template>",
            "    <p>{{ Count + 1 }}</p>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        ViuClassificationKind[] classifications = ClassificationsOnLine(spans, 1);
        classifications.ShouldContain(ViuClassificationKind.InterpolationDelimiter);
        classifications.ShouldContain(ViuClassificationKind.Operator);
        classifications.ShouldContain(ViuClassificationKind.Number);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier).ShouldBe(["Count"]);
        classifications.ShouldNotContain(ViuClassificationKind.Type);
    }

    [Fact]
    public void Classify_TemplateExpressionMemberChain_ClassifiesEveryNameAfterADotAsAMember()
    {
        // The reported defect: Path, Glyph, and Description colored as class names because the
        // PascalCase-is-a-type heuristic claimed them. A name after a dot is a member of what precedes
        // it and can never be a type, in an interpolation and in a binding value alike.
        string[] lines =
        [
            "<template>",
            "    <div :key=\"navigation.Path\">{{ navigation.Target.Glyph }}</div>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier)
            .ShouldBe(["Path", "Target", "Glyph"]);
        ClassificationsOnLine(spans, 1).ShouldNotContain(ViuClassificationKind.Type);
    }

    [Fact]
    public void Classify_TemplateExpressionCallOnAMemberChain_StaysAMethod()
    {
        // Call syntax outranks the member rule: the method pass runs first, so only the invoked name
        // carries the method color while the receiver chain stays members. A class value is exempt
        // from the C# passes entirely — it colors as one utility value — so this probes :title.
        string[] lines =
        [
            "<template>",
            "    <div :title=\"NavigationClass(navigation.Path)\"></div>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Method).ShouldBe(["NavigationClass"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier).ShouldBe(["Path"]);
    }

    [Fact]
    public void Classify_TemplateExpressionChainHead_StaysAvailableToTheTypePass()
    {
        // Only the head of a chain can name a type, and it still reaches the type pass so a static
        // receiver such as DateTime keeps its type color.
        string[] lines =
        [
            "<template>",
            "    <p>{{ DateTime.Now }}</p>",
            "</template>",
        ];

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        SpanTexts(spans, lines, 1, ViuClassificationKind.Type).ShouldBe(["DateTime"]);
        SpanTexts(spans, lines, 1, ViuClassificationKind.Identifier).ShouldBe(["Now"]);
    }

    [Fact]
    public void Classify_ClassAttribute_SplitsUtilityVariantsAndClasses()
    {
        // The lexer keeps variants and classes apart because the language server and the Visual
        // Studio Code grammar act on the distinction; the Visual Studio palette deliberately maps
        // both onto the attribute-value color, which ViuClassificationTypeNamesTests pins.
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

        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.Directive);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.MarkupNode);
        ClassificationsOnLine(spans, 4).ShouldContain(ViuClassificationKind.FrameworkTag);
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

        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 0).ShouldContain(ViuClassificationKind.MarkupAttribute);
        // Custom properties are the theme's tokens and carry their own classification.
        SpanTexts(spans, lines, 1, ViuClassificationKind.StyleCustomProperty)
            .ShouldBe(["--brand-color"]);
        SpanTexts(spans, lines, 2, ViuClassificationKind.StyleSelector).ShouldBe(["button "]);
        ClassificationsOnLine(spans, 2).ShouldContain(ViuClassificationKind.MarkupAttribute);
        ClassificationsOnLine(spans, 3).ShouldContain(ViuClassificationKind.FrameworkTag);
        ClassificationsOnLine(spans, 4).ShouldBeEmpty();
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
