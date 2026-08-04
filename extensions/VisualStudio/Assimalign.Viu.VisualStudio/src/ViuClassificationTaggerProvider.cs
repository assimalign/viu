using System;
using System.Collections.Generic;
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
    /// The one filter is the built-in text document type, not the Viu container types, because a
    /// <c>.viu</c> buffer is not guaranteed to carry the <c>viu</c> content type at all. No editor
    /// registration in Visual Studio claims the <c>.viu</c> file extension, so the file falls through
    /// to the wildcard editor factories, and the XML editor's factory claims any document whose first
    /// token is a well-formed start tag — which a <c>&lt;template&gt;</c> container always is. That
    /// factory attaches the XML language service and suppresses further content-type detection, so the
    /// buffer stays XML-derived for its lifetime and a filter naming <c>viu</c> never matches. See
    /// <c>extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md</c>, "File extension
    /// ownership".
    /// </para>
    /// <para>
    /// Filtering on the text base type is satisfied either way, since both the XML and plain-text
    /// content types derive from it, and <see cref="IsSingleFileComponent"/> then selects containers
    /// here where no editor-factory decision can interfere. Classification therefore works whether or
    /// not <c>.viu</c> has been reassociated with the text editor. This costs one rejected callback
    /// per non-Viu text view.
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
