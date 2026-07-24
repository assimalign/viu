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

    /// <inheritdoc />
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
        ViuClassificationTagger tagger = new(this, textView.Document.Uri);

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
