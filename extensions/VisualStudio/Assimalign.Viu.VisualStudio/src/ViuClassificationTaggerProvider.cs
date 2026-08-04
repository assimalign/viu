using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace Assimalign.Viu.VisualStudio;

#pragma warning disable VSEXTPREVIEW_TAGGERS

/// <summary>
/// Creates lexical classification taggers for Viu single-file components.
/// </summary>
[VisualStudioContribution]
internal sealed class ViuClassificationTaggerProvider :
    ExtensionPart,
    ITextViewTaggerProvider<ClassificationTag>,
    ITextViewChangedListener
{
    private readonly object synchronizationLock = new();
    private readonly Dictionary<Uri, List<ViuClassificationTagger>> taggers = [];
    private readonly TraceSource traceSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViuClassificationTaggerProvider"/> class.
    /// </summary>
    /// <param name="traceSource">
    /// Trace sink supplied by the extension host. Classification runs entirely out of process, so
    /// the trace log is the only channel that can show which spans a document actually received.
    /// </param>
    public ViuClassificationTaggerProvider(TraceSource traceSource)
    {
        this.traceSource = traceSource;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This list must contain exactly one filter, and it must be a document-type filter.
    /// <para>
    /// Visual Studio does not treat <c>AppliesTo</c> as a set of alternatives. It reduces the list to
    /// a single effective document type and a single effective glob — each entry overwrites the
    /// previous one — and then requires <em>both</em> to match. Listing the two container types
    /// therefore does not widen the filter, it narrows it to the last one; adding glob patterns on top
    /// narrows it further to a conjunction that no document can satisfy. A glob-only list is worse
    /// still: the tagger is dropped before it is ever consulted, because the host collects tagger
    /// document types separately and discards a part that declares none.
    /// </para>
    /// <para>
    /// One filter consequently has to cover both containers, which is why
    /// <see cref="ViuLanguageServerProvider.VueCompatibilityDocumentType"/> derives from
    /// <see cref="ViuLanguageServerProvider.ViuDocumentType"/> — a <c>.vue</c> document satisfies this
    /// filter through content-type inheritance. The <c>.vue</c> container is a declared compatibility
    /// target ([V01.01.06.09]) and stays classified.
    /// </para>
    /// <para>
    /// The generator evaluates this property at compile time, so the filter is written inline; the
    /// shipped contract is pinned against the generated manifest instead.
    /// </para>
    /// </remarks>
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = [DocumentFilter.FromDocumentType(ViuLanguageServerProvider.ViuDocumentType)],
    };

    /// <inheritdoc />
    public async Task TextViewChangedAsync(
        TextViewChangedArgs arguments,
        CancellationToken cancellationToken)
    {
        List<Task> updateTasks = [];

        lock (this.synchronizationLock)
        {
            if (this.taggers.TryGetValue(arguments.AfterTextView.Uri, out List<ViuClassificationTagger>? documentTaggers))
            {
                foreach (ViuClassificationTagger tagger in documentTaggers)
                {
                    updateTasks.Add(tagger.TextViewChangedAsync(arguments.AfterTextView, cancellationToken));
                }
            }
        }

        await Task.WhenAll(updateTasks).ConfigureAwait(false);
    }

    Task<TextViewTagger<ClassificationTag>> ITextViewTaggerProvider<ClassificationTag>.CreateTaggerAsync(
        ITextViewSnapshot textView,
        CancellationToken cancellationToken)
    {
        ViuClassificationTagger tagger = new(this, textView.Document.Uri, this.traceSource);

        lock (this.synchronizationLock)
        {
            if (!this.taggers.TryGetValue(textView.Document.Uri, out List<ViuClassificationTagger>? documentTaggers))
            {
                documentTaggers = [];
                this.taggers[textView.Document.Uri] = documentTaggers;
            }

            documentTaggers.Add(tagger);
        }

        return Task.FromResult<TextViewTagger<ClassificationTag>>(tagger);
    }

    internal void RemoveTagger(
        Uri documentUri,
        ViuClassificationTagger tagger)
    {
        lock (this.synchronizationLock)
        {
            if (!this.taggers.TryGetValue(documentUri, out List<ViuClassificationTagger>? documentTaggers))
            {
                return;
            }

            documentTaggers.Remove(tagger);
            if (documentTaggers.Count == 0)
            {
                this.taggers.Remove(documentUri);
            }
        }
    }
}

#pragma warning restore VSEXTPREVIEW_TAGGERS
