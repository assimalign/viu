using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Extensibility.Editor;

namespace Assimalign.Viu.VisualStudio;

#pragma warning disable VSEXTPREVIEW_TAGGERS

internal sealed class ViuClassificationTagger : TextViewTagger<ClassificationTag>
{
    private readonly ViuClassificationTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly TraceSource traceSource;

    public ViuClassificationTagger(
        ViuClassificationTaggerProvider provider,
        Uri documentUri,
        TraceSource traceSource)
    {
        this.provider = provider;
        this.documentUri = documentUri;
        this.traceSource = traceSource;
    }

    public override void Dispose()
    {
        this.provider.RemoveTagger(this.documentUri, this);
        base.Dispose();
    }

    public async Task TextViewChangedAsync(
        ITextViewSnapshot textView,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TextRange> requestedRanges =
            await this.GetAllRequestedRangesAsync(textView.Document, cancellationToken).ConfigureAwait(false);

        await this.CreateTagsAsync(
            textView.Document,
            requestedRanges,
            cancellationToken).ConfigureAwait(false);
    }

    protected override async Task RequestTagsAsync(
        NormalizedTextRangeCollection requestedRanges,
        bool recalculateAll,
        CancellationToken cancellationToken)
    {
        if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot is null)
        {
            return;
        }

        await this.CreateTagsAsync(
            requestedRanges.TextDocumentSnapshot,
            requestedRanges,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateTagsAsync(
        ITextDocumentSnapshot document,
        IEnumerable<TextRange> requestedRanges,
        CancellationToken cancellationToken)
    {
        HashSet<int> requestedLineNumbers = requestedRanges
            .SelectMany(range =>
            {
                int startLine = range.Document.GetLineNumberFromPosition(range.Start);
                int endLine = range.Document.GetLineNumberFromPosition(range.End);
                return Enumerable.Range(startLine, endLine - startLine + 1);
            })
            .ToHashSet();

        if (requestedLineNumbers.Count == 0)
        {
            return;
        }

        List<string> lines = document.Lines
            .Select(line => line.Text.CopyToString())
            .ToList();
        IReadOnlyList<ViuLexicalSpan> lexicalSpans = ViuLexicalClassifier.Classify(lines);
        List<TaggedTrackingTextRange<ClassificationTag>> tags = [];

        foreach (ViuLexicalSpan lexicalSpan in lexicalSpans)
        {
            if (!requestedLineNumbers.Contains(lexicalSpan.LineNumber))
            {
                continue;
            }

            ClassificationTag tag = new(GetClassificationType(lexicalSpan.ClassificationKind));
            var line = document.Lines[lexicalSpan.LineNumber];
            tags.Add(
                new(
                    new(
                        document,
                        line.Text.Start + lexicalSpan.Start,
                        lexicalSpan.Length,
                        TextRangeTrackingMode.ExtendNone),
                    tag));
        }

        List<TextRange> calculatedRanges = requestedLineNumbers
            .OrderBy(lineNumber => lineNumber)
            .Select(lineNumber =>
            {
                var line = document.Lines[lineNumber];
                return new TextRange(
                    document,
                    line.TextIncludingLineBreak.Start,
                    line.TextIncludingLineBreak.Length);
            })
            .ToList();

        this.TraceUpdate(lines.Count, requestedLineNumbers, lexicalSpans, tags.Count);

        await this.UpdateTagsAsync(calculatedRanges, tags, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records which lines Visual Studio asked for and which classifications were produced for them.
    /// </summary>
    /// <remarks>
    /// Classification is computed in the extension process and applied in the Visual Studio process,
    /// so a document that renders uncolored gives no local signal about which half failed. This trace
    /// distinguishes the two: a line absent from <paramref name="requestedLineNumbers"/> was never
    /// asked for, whereas a requested line that contributes no tag was classified as empty.
    /// </remarks>
    private void TraceUpdate(
        int documentLineCount,
        HashSet<int> requestedLineNumbers,
        IReadOnlyList<ViuLexicalSpan> lexicalSpans,
        int tagCount)
    {
        if (!this.traceSource.Switch.ShouldTrace(TraceEventType.Information))
        {
            return;
        }

        string classifications = string.Join(
            " ",
            lexicalSpans
                .Where(span => requestedLineNumbers.Contains(span.LineNumber))
                .GroupBy(span => span.ClassificationKind)
                .OrderByDescending(group => group.Count())
                .Select(group => FormattableString.Invariant($"{group.Key}={group.Count()}")));

        this.traceSource.TraceEvent(
            TraceEventType.Information,
            id: 0,
            format: "Viu classification: {0} lines {1}-{2} of {3} requested, {4} tags [{5}]",
            this.documentUri.LocalPath,
            requestedLineNumbers.Min() + 1,
            requestedLineNumbers.Max() + 1,
            documentLineCount,
            tagCount.ToString(CultureInfo.InvariantCulture),
            classifications);
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
