using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Colors one Viu single-file-component buffer from <see cref="ViuLexicalClassifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole-document lexing, cached per snapshot.</b> A line's classification depends on the
/// container section enclosing it, so the lexer has to see the whole document to color any part of
/// it; asking it for a range would cost the same as asking it for everything. The result is therefore
/// computed once per snapshot and every <see cref="GetClassificationSpans(SnapshotSpan)"/> call is
/// answered by filtering that one result. Exactly one snapshot's spans are held at a time — this is a
/// reuse cache, not a history.
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
internal sealed class ViuClassifier : IClassifier
{
    private readonly ITextBuffer textBuffer;

    /// <summary>
    /// The resolved classification type for each <see cref="ViuClassificationKind"/>, indexed by the
    /// enum value. An entry is <see langword="null"/> only when neither the name nor its fallback
    /// chain is registered, in which case spans of that kind are dropped rather than mis-colored.
    /// </summary>
    private readonly IClassificationType?[] classificationTypes;

    private readonly object classificationLock = new();

    private ITextSnapshot? classifiedSnapshot;
    private IReadOnlyList<ClassificationSpan>? classifiedSpans;

    /// <summary>
    /// Initializes a classifier over one buffer.
    /// </summary>
    /// <param name="textBuffer">The buffer to classify.</param>
    /// <param name="classificationTypeRegistry">The editor's classification type registry.</param>
    public ViuClassifier(
        ITextBuffer textBuffer,
        IClassificationTypeRegistryService classificationTypeRegistry)
    {
        this.textBuffer = textBuffer;
        this.classificationTypes = ResolveClassificationTypes(classificationTypeRegistry);
        this.textBuffer.Changed += this.OnTextBufferChanged;
    }

    /// <inheritdoc />
    public event EventHandler<ClassificationChangedEventArgs>? ClassificationChanged;

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
        lock (this.classificationLock)
        {
            this.classifiedSnapshot = null;
            this.classifiedSpans = null;
        }

        ITextSnapshot snapshot = arguments.After;
        this.ClassificationChanged?.Invoke(
            this,
            new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    private IReadOnlyList<ClassificationSpan> GetSnapshotSpans(ITextSnapshot snapshot)
    {
        lock (this.classificationLock)
        {
            if (ReferenceEquals(this.classifiedSnapshot, snapshot) &&
                this.classifiedSpans is not null)
            {
                return this.classifiedSpans;
            }
        }

        IReadOnlyList<ClassificationSpan> snapshotSpans = this.ClassifySnapshot(snapshot);

        lock (this.classificationLock)
        {
            this.classifiedSnapshot = snapshot;
            this.classifiedSpans = snapshotSpans;
        }

        return snapshotSpans;
    }

    private IReadOnlyList<ClassificationSpan> ClassifySnapshot(ITextSnapshot snapshot)
    {
        IReadOnlyList<ViuLexicalSpan> lexicalSpans =
            ViuLexicalClassifier.Classify(ViuSnapshotLines.Read(snapshot));
        List<ClassificationSpan> snapshotSpans = new(lexicalSpans.Count);

        foreach (ViuLexicalSpan lexicalSpan in lexicalSpans)
        {
            IClassificationType? classificationType =
                this.classificationTypes[(int)lexicalSpan.ClassificationKind];
            if (classificationType is null)
            {
                continue;
            }

            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lexicalSpan.LineNumber);
            int start = line.Start.Position + lexicalSpan.Start;
            if (start + lexicalSpan.Length > snapshot.Length)
            {
                continue;
            }

            snapshotSpans.Add(
                new ClassificationSpan(
                    new SnapshotSpan(snapshot, start, lexicalSpan.Length),
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
    private static IClassificationType?[] ResolveClassificationTypes(
        IClassificationTypeRegistryService classificationTypeRegistry)
    {
        ViuClassificationKind[] classificationKinds =
            (ViuClassificationKind[])Enum.GetValues(typeof(ViuClassificationKind));

        int highestKindValue = 0;
        foreach (ViuClassificationKind classificationKind in classificationKinds)
        {
            if ((int)classificationKind > highestKindValue)
            {
                highestKindValue = (int)classificationKind;
            }
        }

        IClassificationType?[] resolved = new IClassificationType?[highestKindValue + 1];
        foreach (ViuClassificationKind classificationKind in classificationKinds)
        {
            resolved[(int)classificationKind] = ResolveClassificationType(
                classificationTypeRegistry,
                ViuClassificationTypeNames.GetClassificationTypeName(classificationKind));
        }

        return resolved;
    }

    private static IClassificationType? ResolveClassificationType(
        IClassificationTypeRegistryService classificationTypeRegistry,
        string classificationTypeName)
    {
        string? candidateName = classificationTypeName;
        while (candidateName is not null)
        {
            IClassificationType? classificationType =
                classificationTypeRegistry.GetClassificationType(candidateName);
            if (classificationType is not null)
            {
                return classificationType;
            }

            candidateName = ViuClassificationTypeNames.GetFallbackClassificationTypeName(candidateName);
        }

        return null;
    }
}
