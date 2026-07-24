using System;
using System.Collections.Generic;
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

    public ViuClassificationTagger(
        ViuClassificationTaggerProvider provider,
        Uri documentUri)
    {
        this.provider = provider;
        this.documentUri = documentUri;
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

        await this.UpdateTagsAsync(calculatedRanges, tags, cancellationToken).ConfigureAwait(false);
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
            _ => ClassificationType.KnownValues.Text,
        };
}

#pragma warning restore VSEXTPREVIEW_TAGGERS
