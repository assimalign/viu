using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins the container-section attribution both the classifier and the auto-closing decisions read
/// ([V01.01.12.07.08]).
/// </summary>
public class ViuSectionScannerTests
{
    [Fact]
    public void ScanLineSections_HybridContainer_AttributesEveryLineToItsSection()
    {
        // The hybrid .viu container ([V01.01.06.10]): tag-delimited <template>/<style> plus the
        // @script @-block. A line that opens or closes a tag section belongs to it, because content
        // may sit on the same line; the column-0 '}' that ends an @-block does not, because it is
        // structure rather than content.
        string[] lines =
        [
            "<template>",
            "    <div>Hello</div>",
            "</template>",
            "",
            "@script {",
            "    public int Count { get; set; }",
            "}",
            "",
            "<style scoped>",
            "    div { color: red; }",
            "</style>",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
        [
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.None,
            ViuSectionKind.Script,
            ViuSectionKind.Script,
            ViuSectionKind.None,
            ViuSectionKind.None,
            ViuSectionKind.Style,
            ViuSectionKind.Style,
            ViuSectionKind.Style,
        ]);
    }

    [Fact]
    public void ScanLineSections_LegacyAtBlocks_AttributesEveryLineToItsSection()
    {
        // Transition-window pin ([V01.01.06.10]): the legacy @template/@style containers stay
        // recognized until they are removed.
        string[] lines =
        [
            "@template {",
            "    <div>Hello</div>",
            "}",
            "@style scoped {",
            "    div { color: red; }",
            "}",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
        [
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.None,
            ViuSectionKind.Style,
            ViuSectionKind.Style,
            ViuSectionKind.None,
        ]);
    }

    [Fact]
    public void ScanLineSections_NestedTemplateSlotFragment_DoesNotEndTheSection()
    {
        // A <template #header> slot fragment nests inside the container tag of the same name, so
        // only the closer that brings the depth back to zero ends the section.
        string[] lines =
        [
            "<template>",
            "    <template #header>",
            "        <h1>Title</h1>",
            "    </template>",
            "    <p>Body</p>",
            "</template>",
            "trailing",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
        [
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.Template,
            ViuSectionKind.None,
        ]);
    }

    [Fact]
    public void ScanLineSections_UnterminatedOpeningTag_StillOpensTheSection()
    {
        // The state a container is in mid-keystroke: the user has typed "<template" and not yet the
        // '>'. Auto-closing depends on this, because that is exactly when it must decide whether to
        // insert the end tag.
        string[] lines =
        [
            "<template",
            "    <div>",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
            [ViuSectionKind.Template, ViuSectionKind.Template]);
    }

    [Fact]
    public void ScanLineSections_SelfClosingTopLevelTag_OpensNoSection()
    {
        string[] lines =
        [
            "<template />",
            "after",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
            [ViuSectionKind.None, ViuSectionKind.None]);
    }

    [Fact]
    public void ScanLineSections_SectionOpenedAndClosedOnOneLine_ClaimsOnlyThatLine()
    {
        string[] lines =
        [
            "<style>div { color: red; }</style>",
            "after",
            "<template><p>Hello</p></template>",
            "after",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
        [
            ViuSectionKind.Style,
            ViuSectionKind.None,
            ViuSectionKind.Template,
            ViuSectionKind.None,
        ]);
    }

    [Fact]
    public void ScanLineSections_TopLevelScriptTag_IsAttributedToTheScriptSection()
    {
        // The container parser rejects a top-level <script> tag (VIU1017); the editor still treats
        // its body as script so the misplaced code keeps its behavior while the diagnostic points at
        // the fix.
        string[] lines =
        [
            "<script>",
            "    var value = 1;",
            "</script>",
        ];

        ViuSectionScanner.ScanLineSections(lines).ShouldBe(
            [ViuSectionKind.Script, ViuSectionKind.Script, ViuSectionKind.Script]);
    }

    [Fact]
    public void ScanLineSections_EmptyDocument_ReturnsNoSections()
    {
        ViuSectionScanner.ScanLineSections([]).ShouldBeEmpty();
        ViuSectionScanner.ScanLineSections([""]).ShouldBe([ViuSectionKind.None]);
    }

    [Fact]
    public void ScanLineSections_AgreesWithTheClassifiersOwnSectionHandling()
    {
        // The scanner owns the container grammar and the classifier drives its per-line passes from
        // the same primitives; this pins that the two stay in agreement about which section a line
        // is in, read through the classifications only that section can produce.
        string[] lines =
        [
            "<template>",
            "    <div :title=\"Label\">{{ Count }}</div>",
            "</template>",
            "",
            "@script {",
            "    public int Count { get; set; }",
            "}",
            "",
            "<style>",
            "    div { color: red; }",
            "</style>",
        ];

        ViuSectionKind[] sections = ViuSectionScanner.ScanLineSections(lines);
        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(lines);

        sections[1].ShouldBe(ViuSectionKind.Template);
        ClassificationsOnLine(spans, 1).ShouldContain(ViuClassificationKind.MarkupNode);

        sections[5].ShouldBe(ViuSectionKind.Script);
        ClassificationsOnLine(spans, 5).ShouldContain(ViuClassificationKind.Keyword);

        sections[9].ShouldBe(ViuSectionKind.Style);
        ClassificationsOnLine(spans, 9).ShouldContain(ViuClassificationKind.StyleSelector);
    }

    private static IReadOnlyList<ViuClassificationKind> ClassificationsOnLine(
        IReadOnlyList<ViuLexicalSpan> spans,
        int lineNumber) =>
        spans.Where(span => span.LineNumber == lineNumber)
            .Select(span => span.ClassificationKind)
            .ToList();
}
