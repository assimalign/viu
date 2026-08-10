using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Colors one Viu single-file-component buffer from the lexical fallback and exact server-authored
/// semantic classifications.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole-document classification, cached per snapshot.</b> A line's classification depends on the
/// container section enclosing it, so the lexer has to see the whole document to color any part of
/// it; asking it for a range would cost the same as asking it for everything. The result is therefore
/// computed once per snapshot and every <see cref="GetClassificationSpans(SnapshotSpan)"/> call is
/// answered by filtering that one result. Exactly one snapshot's spans are held at a time — this is a
/// reuse cache, not a history.
/// </para>
/// <para>
/// <b>Semantic overlay.</b> The language server publishes authored ranges with the exact C# editor
/// classification-type names. A publication is used only when its text checksum matches this
/// snapshot; otherwise the lexical result remains intact. Exact spans replace intersecting lexical
/// identifier spans, so the editor never receives two competing classifications for one token.
/// </para>
/// <para>
/// <b>Change notification is whole-buffer</b> for the same reason: adding a <c>&lt;/template&gt;</c>
/// on line three changes how line three hundred is colored, so no narrower invalidation would be
/// correct. The editor re-requests only the ranges it is actually displaying, so the cost of a wide
/// notification is bounded by the visible text rather than by the document.
/// </para>
/// <para>
/// <b>Lifetime.</b> The classifier is stored in the buffer's property collection and subscribes to
/// that same buffer, so the subscription is a self-reference that dies with the buffer;
/// <see cref="IClassifier"/> has no disposal point to unsubscribe from.
/// </para>
/// </remarks>
internal sealed class ViuClassifier : IClassifier, IViuSemanticClassificationListener
{
    private readonly ITextBuffer textBuffer;
    private readonly IClassificationTypeRegistryService classificationTypeRegistry;
    private readonly ViuSemanticClassificationState semanticClassificationState;
    private readonly ITextDocumentFactoryService textDocumentFactory;
    private ITextDocument? textDocument;
    private string? documentIdentifier;

    /// <summary>
    /// The resolved classification type for each <see cref="ViuClassificationKind"/>, indexed by the
    /// enum value. An entry is <see langword="null"/> only when neither the name nor its fallback
    /// chain is registered, in which case spans of that kind are dropped rather than mis-colored.
    /// </summary>
    private readonly Dictionary<string, IClassificationType?> classificationTypes =
        new(StringComparer.Ordinal);

    private readonly object classificationLock = new();
    private readonly object classificationTypeLock = new();

    private ITextSnapshot? classifiedSnapshot;
    private IReadOnlyList<ClassificationSpan>? classifiedSpans;
    private long classificationGeneration;

    /// <summary>
    /// Initializes a classifier over one buffer.
    /// </summary>
    /// <param name="textBuffer">The buffer to classify.</param>
    /// <param name="classificationTypeRegistry">The editor's classification type registry.</param>
    /// <param name="semanticClassificationState">The server publication state.</param>
    /// <param name="textDocumentFactory">
    /// The document service used only to associate the buffer with the URI published by the server.
    /// </param>
    public ViuClassifier(
        ITextBuffer textBuffer,
        IClassificationTypeRegistryService classificationTypeRegistry,
        ViuSemanticClassificationState semanticClassificationState,
        ITextDocumentFactoryService textDocumentFactory)
    {
        this.textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
        this.classificationTypeRegistry = classificationTypeRegistry ??
            throw new ArgumentNullException(nameof(classificationTypeRegistry));
        this.semanticClassificationState = semanticClassificationState ??
            throw new ArgumentNullException(nameof(semanticClassificationState));
        this.textDocumentFactory = textDocumentFactory ??
            throw new ArgumentNullException(nameof(textDocumentFactory));
        this.PrimeLexicalClassificationTypes();
        this.textBuffer.Changed += this.OnTextBufferChanged;
        this.EnsureDocumentIdentifier();
    }

    /// <inheritdoc />
    public event EventHandler<ClassificationChangedEventArgs>? ClassificationChanged;

    /// <inheritdoc />
    public void OnSemanticClassificationsChanged(string changedDocumentIdentifier)
    {
        this.InvalidateClassificationCache();
        ITextSnapshot snapshot = this.textBuffer.CurrentSnapshot;
        this.ClassificationChanged?.Invoke(
            this,
            new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    /// <inheritdoc />
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
    {
        List<ClassificationSpan> intersectingSpans = [];
        if (span.Snapshot.Length == 0)
        {
            return intersectingSpans;
        }

        foreach (ClassificationSpan classificationSpan in this.GetSnapshotSpans(span.Snapshot))
        {
            if (classificationSpan.Span.IntersectsWith(span))
            {
                intersectingSpans.Add(classificationSpan);
            }
        }

        return intersectingSpans;
    }

    private void OnTextBufferChanged(object sender, TextContentChangedEventArgs arguments)
    {
        this.InvalidateClassificationCache();

        ITextSnapshot snapshot = arguments.After;
        this.ClassificationChanged?.Invoke(
            this,
            new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    private IReadOnlyList<ClassificationSpan> GetSnapshotSpans(ITextSnapshot snapshot)
    {
        this.EnsureDocumentIdentifier();
        long generation;
        lock (this.classificationLock)
        {
            if (ReferenceEquals(this.classifiedSnapshot, snapshot) &&
                this.classifiedSpans is not null)
            {
                return this.classifiedSpans;
            }

            generation = this.classificationGeneration;
        }

        IReadOnlyList<ClassificationSpan> snapshotSpans = this.ClassifySnapshot(snapshot);

        lock (this.classificationLock)
        {
            // A server publication can arrive while classification runs. Do not let an older result
            // repopulate the cache after that publication invalidated it.
            if (generation == this.classificationGeneration)
            {
                this.classifiedSnapshot = snapshot;
                this.classifiedSpans = snapshotSpans;
            }
        }

        return snapshotSpans;
    }

    private IReadOnlyList<ClassificationSpan> ClassifySnapshot(ITextSnapshot snapshot)
    {
        IReadOnlyList<ViuLexicalSpan> lexicalSpans =
            ViuLexicalClassifier.Classify(ViuSnapshotLines.Read(snapshot));
        IReadOnlyList<ViuSemanticClassification> semanticClassifications =
            Array.Empty<ViuSemanticClassification>();
        string? associatedDocumentIdentifier = Volatile.Read(ref this.documentIdentifier);
        if (associatedDocumentIdentifier is not null)
        {
            string checksum = ViuDocumentTextChecksum.Compute(snapshot.GetText());
            this.semanticClassificationState.TryGetClassifications(
                associatedDocumentIdentifier,
                checksum,
                out semanticClassifications);
        }

        IReadOnlyList<ViuResolvedClassificationSpan> resolvedSpans =
            ViuClassificationMerge.Merge(lexicalSpans, semanticClassifications);
        List<ClassificationSpan> snapshotSpans = new(resolvedSpans.Count);

        foreach (ViuResolvedClassificationSpan resolvedSpan in resolvedSpans)
        {
            IClassificationType? classificationType =
                this.GetOrResolveClassificationType(resolvedSpan.ClassificationTypeName);
            if (classificationType is null)
            {
                continue;
            }

            if (resolvedSpan.LineNumber < 0 ||
                resolvedSpan.LineNumber >= snapshot.LineCount ||
                resolvedSpan.Start < 0 ||
                resolvedSpan.Length <= 0)
            {
                continue;
            }

            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(resolvedSpan.LineNumber);
            if (resolvedSpan.Start + resolvedSpan.Length > line.Length)
            {
                continue;
            }

            int start = line.Start.Position + resolvedSpan.Start;

            snapshotSpans.Add(
                new ClassificationSpan(
                    new SnapshotSpan(snapshot, start, resolvedSpan.Length),
                    classificationType));
        }

        return snapshotSpans;
    }

    /// <summary>
    /// Resolves every kind's classification type once, walking the fallback chain when a name is
    /// absent.
    /// </summary>
    /// <remarks>
    /// Resolution happens once per buffer rather than per span, and it is defensive on purpose: the
    /// Roslyn-contributed names (<c>method name</c>, <c>class name</c>, <c>punctuation</c>) are
    /// present in any installation with a managed-language workload but are not part of the core
    /// editor, and Viu templates must still color without one.
    /// </remarks>
    private void PrimeLexicalClassificationTypes()
    {
        ViuClassificationKind[] classificationKinds =
            (ViuClassificationKind[])Enum.GetValues(typeof(ViuClassificationKind));
        foreach (ViuClassificationKind classificationKind in classificationKinds)
        {
            this.GetOrResolveClassificationType(
                ViuClassificationTypeNames.GetClassificationTypeName(classificationKind));
        }
    }

    private IClassificationType? GetOrResolveClassificationType(string classificationTypeName)
    {
        lock (this.classificationTypeLock)
        {
            if (this.classificationTypes.TryGetValue(classificationTypeName, out var cached))
            {
                return cached;
            }

            string? candidateName = classificationTypeName;
            while (candidateName is not null)
            {
                IClassificationType? classificationType =
                    this.classificationTypeRegistry.GetClassificationType(candidateName);
                if (classificationType is not null)
                {
                    this.classificationTypes.Add(classificationTypeName, classificationType);
                    return classificationType;
                }

                candidateName =
                    ViuClassificationTypeNames.GetFallbackClassificationTypeName(candidateName);
            }

            this.classificationTypes.Add(classificationTypeName, null);
            return null;
        }
    }

    private void InvalidateClassificationCache()
    {
        lock (this.classificationLock)
        {
            this.classificationGeneration++;
            this.classifiedSnapshot = null;
            this.classifiedSpans = null;
        }
    }

    private void EnsureDocumentIdentifier()
    {
        ITextDocument? associatedDocument = Volatile.Read(ref this.textDocument);
        if (associatedDocument is null)
        {
            if (!this.textDocumentFactory.TryGetTextDocument(
                    this.textBuffer,
                    out ITextDocument discoveredDocument))
            {
                return;
            }

            associatedDocument = Interlocked.CompareExchange(
                    ref this.textDocument,
                    discoveredDocument,
                    null) ??
                discoveredDocument;
        }

        string filePath = associatedDocument.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.Equals(
                Volatile.Read(ref this.documentIdentifier),
                filePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? previous = Interlocked.Exchange(ref this.documentIdentifier, filePath);
        if (string.Equals(previous, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.semanticClassificationState.Subscribe(filePath, this);
        this.InvalidateClassificationCache();
    }
}
