using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

#pragma warning disable VSEXTPREVIEW_TAGGERS

/// <summary>
/// Pins how much of a document the tagger reports and how often it re-lexes it.
/// </summary>
/// <remarks>
/// <para>
/// The extension runs out of process, so every report is a JSON-RPC round trip and Visual Studio
/// draws a region in the default text color until one completes. Reporting only the lines Visual
/// Studio asked about therefore made scrolling flicker: newly revealed lines had never been
/// calculated on the Visual Studio side. These tests pin the fix — one report covering the whole
/// document — and the two guards that keep the wider payload from costing more than it saves.
/// </para>
/// <para>
/// They exercise the tagger through its offset-based overload. The editor's document snapshot type
/// carries internal members and cannot be implemented outside the Visual Studio SDK, so the thin
/// adapter that turns a snapshot into these offsets, and the spans into tracking ranges, is the one
/// part of the path that only a running Visual Studio can exercise. Seeing the flicker itself is
/// likewise only possible there.
/// </para>
/// </remarks>
public class ViuClassificationTaggerTests
{
    private static readonly string[] ContainerLines =
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

    /// <summary>
    /// The requested ranges are deliberately ignored. Reporting a calculated range that spans the
    /// whole document is what lets Visual Studio answer a later scroll from its own cache instead of
    /// waiting on another round trip.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_ContainerDocument_ReportsACalculatedRangeSpanningTheWholeDocument()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await CreateTagsAsync(tagger, reporter, ContainerLines);

        ClassificationReport report = reporter.Reports.ShouldHaveSingleItem();
        report.CalculatedRangeStart.ShouldBe(0);
        report.CalculatedRangeLength.ShouldBe(DocumentLength(ContainerLines));
    }

    /// <summary>
    /// Every classified span is reported, not only the ones on lines Visual Studio happened to ask
    /// about — including spans on the last line of the document, which a visible-range filter used to
    /// drop until the user scrolled there.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_ContainerDocument_ReportsEverySpanIncludingTheLastLine()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await CreateTagsAsync(tagger, reporter, ContainerLines);

        IReadOnlyList<ViuLexicalSpan> reportedSpans = reporter.Reports.ShouldHaveSingleItem().LexicalSpans;
        reportedSpans.ShouldBe(ViuLexicalClassifier.Classify(ContainerLines));
        reportedSpans.Select(span => span.LineNumber).ShouldContain(0);
        reportedSpans.Select(span => span.LineNumber).ShouldContain(ContainerLines.Length - 1);
    }

    /// <summary>
    /// The provider applies to every text document, because a <c>.viu</c> buffer is not guaranteed to
    /// carry the Viu content type, so a tagger exists for documents Viu does not own. Those report
    /// nothing at all — not an empty tag set — and never read or lex unrelated source.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_NonContainerDocument_ReportsNothingAndNeverReadsTheDocument()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: false);

        await tagger.CreateTagsAsync(
            documentVersion: 1,
            documentLength: DocumentLength(ContainerLines),
            readDocumentLines: () => throw new ShouldAssertException(
                "A document Viu does not own must never be read."),
            reportAsync: reporter.ReportAsync,
            recalculateAll: false,
            CancellationToken.None);

        reporter.Reports.ShouldBeEmpty();
        tagger.ClassificationRunCount.ShouldBe(0);
    }

    /// <summary>
    /// Visual Studio raises a text-view change and then requests tags for the same snapshot, so one
    /// keystroke reaches the tagger twice. The second pass must neither re-lex nor spend a round
    /// trip: <c>recalculateAll</c> is the documented signal that previously reported tags are
    /// outdated, and it is absent here.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_RepeatedRequestForTheSameDocumentVersion_ReportsOnceAndClassifiesOnce()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await CreateTagsAsync(tagger, reporter, ContainerLines);
        await CreateTagsAsync(tagger, reporter, ContainerLines);

        reporter.Reports.Count.ShouldBe(1);
        tagger.ClassificationRunCount.ShouldBe(1);
    }

    /// <summary>
    /// When Visual Studio declares its copy outdated the tags have to go out again, but the snapshot
    /// has not changed, so the cached lexer result is reused.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_RecalculateAll_ReportsAgainWithoutReclassifying()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await CreateTagsAsync(tagger, reporter, ContainerLines);
        await CreateTagsAsync(tagger, reporter, ContainerLines, recalculateAll: true);

        reporter.Reports.Count.ShouldBe(2);
        tagger.ClassificationRunCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTagsAsync_NewDocumentVersion_ReclassifiesAndReportsAgain()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await CreateTagsAsync(tagger, reporter, ContainerLines, documentVersion: 1);
        await CreateTagsAsync(tagger, reporter, ContainerLines, documentVersion: 2);

        reporter.Reports.Count.ShouldBe(2);
        tagger.ClassificationRunCount.ShouldBe(2);
    }

    /// <summary>
    /// A calculated range may not be empty, and an empty document offers no other range to report and
    /// no position a tracking range could survive on.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_EmptyDocument_ReportsNothing()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await tagger.CreateTagsAsync(
            documentVersion: 1,
            documentLength: 0,
            readDocumentLines: () => [],
            reportAsync: reporter.ReportAsync,
            recalculateAll: false,
            CancellationToken.None);

        reporter.Reports.ShouldBeEmpty();
    }

    /// <summary>
    /// The version watermark records a delivery, not an attempt: a report that failed on the way to
    /// Visual Studio must not leave the document permanently uncolored.
    /// </summary>
    [Fact]
    public async Task CreateTagsAsync_FailedReport_LeavesTheNextRequestFreeToReportAgain()
    {
        RecordingReporter reporter = new();
        ViuClassificationTagger tagger = CreateTagger(isSingleFileComponent: true);

        await Should.ThrowAsync<InvalidOperationException>(
            tagger.CreateTagsAsync(
                documentVersion: 1,
                documentLength: DocumentLength(ContainerLines),
                readDocumentLines: () => ContainerLines,
                reportAsync: (_, _, _, _) => throw new InvalidOperationException("connection lost"),
                recalculateAll: false,
                CancellationToken.None));

        await CreateTagsAsync(tagger, reporter, ContainerLines, documentVersion: 1);

        reporter.Reports.Count.ShouldBe(1);
        // The failed attempt already lexed version 1, so the retry reuses the cached spans.
        tagger.ClassificationRunCount.ShouldBe(1);
    }

    /// <summary>
    /// Creates a tagger with no provider: an <c>ExtensionPart</c> resolves host services in its
    /// constructor and cannot exist outside Visual Studio, and the tagger consults its provider only
    /// while unregistering itself on disposal, which these tests do not exercise.
    /// </summary>
    private static ViuClassificationTagger CreateTagger(bool isSingleFileComponent) =>
        new(
            provider: null!,
            new Uri("file:///C:/Source/repos/app/Components/FeatureCard.viu"),
            isSingleFileComponent);

    private static Task CreateTagsAsync(
        ViuClassificationTagger tagger,
        RecordingReporter reporter,
        string[] lines,
        int documentVersion = 1,
        bool recalculateAll = false) =>
        tagger.CreateTagsAsync(
            documentVersion,
            DocumentLength(lines),
            () => lines,
            reporter.ReportAsync,
            recalculateAll,
            CancellationToken.None);

    /// <summary>
    /// Length of the document the lines form, counting the two-character line breaks between them.
    /// </summary>
    private static int DocumentLength(string[] lines) =>
        lines.Sum(line => line.Length) + (Math.Max(lines.Length - 1, 0) * 2);

    private sealed class RecordingReporter
    {
        public List<ClassificationReport> Reports { get; } = [];

        public Task ReportAsync(
            int calculatedRangeStart,
            int calculatedRangeLength,
            IReadOnlyList<ViuLexicalSpan> lexicalSpans,
            CancellationToken cancellationToken)
        {
            this.Reports.Add(new ClassificationReport(calculatedRangeStart, calculatedRangeLength, lexicalSpans));
            return Task.CompletedTask;
        }
    }

    private sealed record ClassificationReport(
        int CalculatedRangeStart,
        int CalculatedRangeLength,
        IReadOnlyList<ViuLexicalSpan> LexicalSpans);
}

#pragma warning restore VSEXTPREVIEW_TAGGERS
