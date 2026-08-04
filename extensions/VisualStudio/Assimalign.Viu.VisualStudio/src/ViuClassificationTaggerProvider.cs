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

    /// <summary>
    /// Determines whether a document is a Viu single-file component from its file name.
    /// </summary>
    /// <remarks>
    /// The tagger applies to every text document, so it has to recognize its own documents itself
    /// rather than relying on Visual Studio to filter them — see
    /// <see cref="TextViewExtensionConfiguration"/>. Matching is on file extension because that is the
    /// only signal available before any container parsing.
    /// </remarks>
    internal static bool IsSingleFileComponent(Uri documentUri) =>
        documentUri.LocalPath.EndsWith(".viu", StringComparison.OrdinalIgnoreCase)
        || documentUri.LocalPath.EndsWith(".vue", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <remarks>
    /// This list must contain exactly one filter, and it must be a document-type filter.
    /// <para>
    /// Visual Studio does not treat <c>AppliesTo</c> as a set of alternatives. It reduces the list to
    /// a single effective document type and a single effective glob — each entry overwrites the
    /// previous one — and then requires <em>both</em> to match. Listing two container types therefore
    /// does not widen the filter, it narrows it to the last one; adding glob patterns narrows it
    /// further to a conjunction no document can satisfy. A glob-only list is worse still: the tagger is
    /// dropped before it is ever consulted, because the host collects tagger document types separately
    /// and discards a part that declares none.
    /// </para>
    /// <para>
    /// The one filter is the built-in text document type, not the Viu container types. Those exist
    /// only once this extension has loaded and its <c>documentTypes</c> have been applied to the
    /// content-type registry at runtime; nothing static registers the <c>.viu</c> extension, so a
    /// filter naming them cannot match a document whose buffer was created first, and the tagger is
    /// never asked for it. Filtering on the text base type is always satisfiable, and
    /// <see cref="IsSingleFileComponent"/> then does the container selection here, where it cannot be
    /// lost to a registration race. This costs one rejected callback per non-Viu text view.
    /// </para>
    /// <para>
    /// The generator evaluates this property at compile time, so the filter is written inline; the
    /// shipped contract is pinned against the generated manifest instead.
    /// </para>
    /// </remarks>
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = [DocumentFilter.FromDocumentType(DocumentType.KnownValues.Text)],
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
        // Every text document reaches this method, so a tagger is always returned - the contract has
        // no way to decline - but one created for a document that is not a Viu container produces no
        // tags and is not tracked for change notifications.
        bool isSingleFileComponent = IsSingleFileComponent(textView.Document.Uri);
        ViuClassificationTagger tagger = new(
            this,
            textView.Document.Uri,
            this.traceSource,
            isSingleFileComponent);

        if (isSingleFileComponent)
        {
            lock (this.synchronizationLock)
            {
                if (!this.taggers.TryGetValue(textView.Document.Uri, out List<ViuClassificationTagger>? documentTaggers))
                {
                    documentTaggers = [];
                    this.taggers[textView.Document.Uri] = documentTaggers;
                }

                documentTaggers.Add(tagger);
            }
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
