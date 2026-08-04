using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Extensibility.Editor;

namespace Assimalign.Viu.VisualStudio;

#pragma warning disable VSEXTPREVIEW_TAGGERS

/// <summary>
/// Reports lexical classification for one Viu single-file component text view.
/// </summary>
/// <remarks>
/// Every report covers the whole document rather than only the ranges Visual Studio asked about —
/// see <see cref="CreateTagsAsync(int, int, Func{IReadOnlyList{string}}, ViuClassificationReportHandler, bool, CancellationToken)"/>
/// for why — and the lexer result is cached on the document version so one snapshot is lexed once.
/// </remarks>
internal sealed class ViuClassificationTagger : TextViewTagger<ClassificationTag>
{
    /// <summary>
    /// Version sentinel meaning "nothing classified or reported yet". Document versions are
    /// non-negative, so this cannot collide with a real snapshot.
    /// </summary>
    private const int NoDocumentVersion = -1;

    private readonly ViuClassificationTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly bool isSingleFileComponent;
    private readonly object classificationLock = new();

    private int classifiedDocumentVersion = NoDocumentVersion;
    private IReadOnlyList<ViuLexicalSpan>? classifiedSpans;
    private int reportedDocumentVersion = NoDocumentVersion;

    public ViuClassificationTagger(
        ViuClassificationTaggerProvider provider,
        Uri documentUri,
        bool isSingleFileComponent)
    {
        this.provider = provider;
        this.documentUri = documentUri;
        this.isSingleFileComponent = isSingleFileComponent;
    }

    /// <summary>
    /// Gets the number of times this tagger has actually run the lexer over its document.
    /// </summary>
    /// <remarks>
    /// Caching is only observable as a run count, so this counter exists for the tests that pin it.
    /// </remarks>
    internal int ClassificationRunCount { get; private set; }

    public override void Dispose()
    {
        if (this.isSingleFileComponent)
        {
            this.provider.RemoveTagger(this.documentUri, this);
        }

        base.Dispose();
    }

    public Task TextViewChangedAsync(
        ITextViewSnapshot textView,
        CancellationToken cancellationToken) =>
        // No call to GetAllRequestedRangesAsync: that is the SDK's way of re-reporting exactly the
        // ranges asked for so far, which costs a round trip of its own and is a strict subset of the
        // whole document reported here anyway. A view change that left the text alone does not move
        // the document version and is dropped by the version check below.
        this.CreateTagsAsync(textView.Document, recalculateAll: false, cancellationToken);

    protected override Task RequestTagsAsync(
        NormalizedTextRangeCollection requestedRanges,
        bool recalculateAll,
        CancellationToken cancellationToken)
    {
        // The requested ranges themselves are deliberately unused - the whole document is reported
        // regardless of which part of it Visual Studio asked about. The collection is still the only
        // carrier of the snapshot the request refers to, and it holds one only while it is non-empty.
        if (requestedRanges.TextDocumentSnapshot is not { } document)
        {
            return Task.CompletedTask;
        }

        return this.CreateTagsAsync(document, recalculateAll, cancellationToken);
    }

    /// <summary>
    /// Classifies a document in full and reports it as a single calculated range.
    /// </summary>
    /// <param name="documentVersion">Version of the snapshot being classified.</param>
    /// <param name="documentLength">Length of the snapshot in characters.</param>
    /// <param name="readDocumentLines">Materializes the snapshot's lines; called only when the
    /// document actually has to be lexed.</param>
    /// <param name="reportAsync">Delivers the calculated range and the spans inside it.</param>
    /// <param name="recalculateAll">Whether Visual Studio considers every previously reported tag
    /// outdated.</param>
    /// <param name="cancellationToken">Cancellation token for the async call.</param>
    /// <remarks>
    /// <para>
    /// Visual Studio caches what a tagger reports. <c>UpdateTagsAsync</c> takes the calculated ranges
    /// separately from the tags precisely so the range set can say "this region has an answer"
    /// independently of whether that answer contained any tags, and the SDK documents the
    /// consequence: "all previous tags that are fully overlapping <c>updatedRanges</c> will be
    /// considered outdated". The companion <c>GetAllRequestedRangesAsync</c> — "gets all the ranges
    /// for which tags have been requested so far" — exists only because that cache survives across
    /// requests: without a host-side cache there would be nothing for an extension to refresh.
    /// </para>
    /// <para>
    /// This extension runs out of process, so every request and every report is a JSON-RPC round
    /// trip. Reporting only the requested lines therefore left every line the user had not scrolled
    /// to yet uncalculated on the Visual Studio side; scrolling drew those lines in the default text
    /// color until the round trip completed, which is the reported colorization flicker. Viu
    /// classification is whole-document lexing already — the lexer must see the container sections
    /// above a line to know how to color it — so widening the report costs nothing to compute. The
    /// whole document goes out at once and later scrolling is answered from Visual Studio's own cache
    /// with no round trip at all.
    /// </para>
    /// <para>
    /// Two cheap guards keep the wider payload from costing more than it saves: the lexer result is
    /// cached on the document version, and a version already delivered is not sent again unless
    /// Visual Studio says its copy is stale.
    /// </para>
    /// </remarks>
    internal async Task CreateTagsAsync(
        int documentVersion,
        int documentLength,
        Func<IReadOnlyList<string>> readDocumentLines,
        ViuClassificationReportHandler reportAsync,
        bool recalculateAll,
        CancellationToken cancellationToken)
    {
        // The provider applies to every text document, so a tagger exists for documents Viu does not
        // own. Those produce no tags rather than running the Viu lexer over unrelated source.
        if (!this.isSingleFileComponent)
        {
            return;
        }

        lock (this.classificationLock)
        {
            // recalculateAll is documented as "whether all previous tags generated by this tagger
            // should be considered outdated", so its absence means Visual Studio still holds what was
            // last reported for this snapshot and a repeat request needs no round trip.
            if (!recalculateAll && this.reportedDocumentVersion == documentVersion)
            {
                return;
            }
        }

        // A calculated range may not be empty, and an empty document has no text to color and no
        // position a tracking range could survive on.
        if (documentLength == 0)
        {
            return;
        }

        IReadOnlyList<ViuLexicalSpan> lexicalSpans = this.ClassifyDocument(documentVersion, readDocumentLines);

        await reportAsync(0, documentLength, lexicalSpans, cancellationToken).ConfigureAwait(false);

        lock (this.classificationLock)
        {
            // Recorded only once the report has been delivered, so a cancelled or failed update
            // cannot suppress the next request. A report for an older snapshot that lands after a
            // newer one must not walk the watermark backwards either.
            this.reportedDocumentVersion = Math.Max(this.reportedDocumentVersion, documentVersion);
        }
    }

    /// <summary>
    /// Adapts a Visual Studio snapshot onto the classification policy above.
    /// </summary>
    /// <remarks>
    /// Everything the policy needs is reduced to a version, a length, the document's lines, and a
    /// report handler, because <see cref="ITextDocumentSnapshot"/> carries internal members and
    /// cannot be implemented outside the Visual Studio SDK. Only this method and the two helpers it
    /// uses touch the editor types.
    /// </remarks>
    private Task CreateTagsAsync(
        ITextDocumentSnapshot document,
        bool recalculateAll,
        CancellationToken cancellationToken) =>
        this.CreateTagsAsync(
            document.RpcContract.Version,
            document.Length,
            () => ReadDocumentLines(document),
            (calculatedRangeStart, calculatedRangeLength, lexicalSpans, token) =>
                this.UpdateTagsAsync(
                    [new TextRange(document, calculatedRangeStart, calculatedRangeLength)],
                    CreateTags(document, lexicalSpans),
                    token),
            recalculateAll,
            cancellationToken);

    /// <summary>
    /// Lexes the document, reusing the previous result while the snapshot has not changed.
    /// </summary>
    /// <remarks>
    /// Visual Studio raises a text-view change and then requests tags for the same snapshot, so one
    /// keystroke reaches the tagger twice. The cache makes the second pass free without keeping any
    /// history — one version is held at a time.
    /// </remarks>
    private IReadOnlyList<ViuLexicalSpan> ClassifyDocument(
        int documentVersion,
        Func<IReadOnlyList<string>> readDocumentLines)
    {
        lock (this.classificationLock)
        {
            if (this.classifiedDocumentVersion == documentVersion && this.classifiedSpans is not null)
            {
                return this.classifiedSpans;
            }
        }

        IReadOnlyList<ViuLexicalSpan> lexicalSpans = ViuLexicalClassifier.Classify(readDocumentLines());

        lock (this.classificationLock)
        {
            this.classifiedDocumentVersion = documentVersion;
            this.classifiedSpans = lexicalSpans;
            this.ClassificationRunCount++;
        }

        return lexicalSpans;
    }

    private static IReadOnlyList<string> ReadDocumentLines(ITextDocumentSnapshot document)
    {
        IReadOnlyList<ITextDocumentSnapshotLine> documentLines = document.Lines;
        List<string> lines = new(documentLines.Count);
        foreach (ITextDocumentSnapshotLine documentLine in documentLines)
        {
            lines.Add(documentLine.Text.CopyToString());
        }

        return lines;
    }

    private static List<TaggedTrackingTextRange<ClassificationTag>> CreateTags(
        ITextDocumentSnapshot document,
        IReadOnlyList<ViuLexicalSpan> lexicalSpans)
    {
        IReadOnlyList<ITextDocumentSnapshotLine> documentLines = document.Lines;
        List<TaggedTrackingTextRange<ClassificationTag>> tags = new(lexicalSpans.Count);

        foreach (ViuLexicalSpan lexicalSpan in lexicalSpans)
        {
            ClassificationTag tag = new(GetClassificationType(lexicalSpan.ClassificationKind));
            ITextDocumentSnapshotLine line = documentLines[lexicalSpan.LineNumber];
            tags.Add(
                new(
                    new(
                        document,
                        line.Text.Start + lexicalSpan.Start,
                        lexicalSpan.Length,
                        TextRangeTrackingMode.ExtendNone),
                    tag));
        }

        return tags;
    }

    internal static ClassificationType GetClassificationType(
        ViuClassificationKind classificationKind) =>
        classificationKind switch
        {
            ViuClassificationKind.Keyword => ClassificationType.KnownValues.Keyword,
            ViuClassificationKind.Comment => ClassificationType.KnownValues.Comment,
            ViuClassificationKind.Identifier => ClassificationType.KnownValues.Identifier,
            ViuClassificationKind.MarkupAttribute => ClassificationType.KnownValues.MarkupAttribute,
            ViuClassificationKind.MarkupAttributeValue => ClassificationType.KnownValues.MarkupAttributeValue,
            ViuClassificationKind.MarkupNode => ClassificationType.KnownValues.MarkupNode,
            // Visual Studio 18's out-of-process editor bridge does not register the SDK's
            // "method" classification name. Identifier retains semantic coloring without
            // causing RemoteTagConversionUtilities to reject the tag.
            ViuClassificationKind.Method => ClassificationType.KnownValues.Identifier,
            ViuClassificationKind.Number => ClassificationType.KnownValues.Number,
            ViuClassificationKind.Operator => ClassificationType.KnownValues.Operator,
            // "punctuation" is contributed by Roslyn rather than the base Visual Studio editor.
            // Operator is available without requiring a particular managed-language workload.
            ViuClassificationKind.Punctuation => ClassificationType.KnownValues.Operator,
            ViuClassificationKind.String => ClassificationType.KnownValues.String,
            ViuClassificationKind.Type => ClassificationType.KnownValues.Type,
            // Components borrow the "type" category so PascalCase tags render in the same teal
            // Visual Studio uses for C# and Razor type names; directives, interpolation delimiters,
            // and utility variants borrow "keyword" and utility classes borrow "string". The
            // out-of-process model cannot register custom classification types, so every Viu token
            // must map onto a built-in category the base editor registers (see docs/DESIGN.md).
            ViuClassificationKind.Component => ClassificationType.KnownValues.Type,
            ViuClassificationKind.Directive => ClassificationType.KnownValues.Keyword,
            ViuClassificationKind.InterpolationDelimiter => ClassificationType.KnownValues.Keyword,
            ViuClassificationKind.UtilityVariant => ClassificationType.KnownValues.Keyword,
            ViuClassificationKind.UtilityClass => ClassificationType.KnownValues.String,
            _ => ClassificationType.KnownValues.Text,
        };
}

#pragma warning restore VSEXTPREVIEW_TAGGERS
