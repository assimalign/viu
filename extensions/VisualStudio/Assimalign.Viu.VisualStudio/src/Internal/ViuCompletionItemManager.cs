using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Delegates filtering and sorting to Visual Studio and changes only candidate-specific color icons.
/// </summary>
internal sealed class ViuCompletionItemManager : IAsyncCompletionItemManager
{
    private readonly IAsyncCompletionItemManager inner;
    private readonly ViuCompletionDocumentPath documentPath;
    private readonly ViuCompletionColorState state;
    private readonly ViuCompletionColorImageCatalog imageCatalog;

    /// <summary>Initializes a presentation-only wrapper around the stock manager.</summary>
    internal ViuCompletionItemManager(
        IAsyncCompletionItemManager inner,
        ViuCompletionDocumentPath documentPath,
        ViuCompletionColorState state,
        ViuCompletionColorImageCatalog imageCatalog)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.documentPath = documentPath ?? throw new ArgumentNullException(nameof(documentPath));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.imageCatalog = imageCatalog ?? throw new ArgumentNullException(nameof(imageCatalog));
    }

    /// <inheritdoc />
    public async Task<ImmutableArray<CompletionItem>> SortCompletionListAsync(
        IAsyncCompletionSession session,
        AsyncCompletionSessionInitialDataSnapshot data,
        CancellationToken token)
    {
        ImmutableArray<CompletionItem> sorted =
            await this.inner.SortCompletionListAsync(session, data, token).ConfigureAwait(false);
        List<CompletionSourceGroup> sourceGroups = CreateSourceGroups(sorted);
        var candidateGroups = new List<IReadOnlyList<ViuCompletionCandidateIdentity>>(
            sourceGroups.Count);
        foreach (CompletionSourceGroup sourceGroup in sourceGroups)
        {
            candidateGroups.Add(sourceGroup.Candidates);
        }

        if (!this.state.TryTakeMatchingPublication(
                this.documentPath.Current,
                candidateGroups,
                out int sourceGroupIndex,
                out var publication))
        {
            return sorted;
        }

        CompletionSourceGroup matchingGroup = sourceGroups[sourceGroupIndex];
        var decorations = new List<CompletionDecoration>();

        for (var index = 0; index < sorted.Length; index++)
        {
            CompletionItem item = sorted[index];
            if (!ReferenceEquals(item.Source, matchingGroup.Source) ||
                !publication!.Presentations.TryGetValue(
                    CreateIdentity(item),
                    out var presentation))
            {
                continue;
            }

            decorations.Add(new CompletionDecoration(index, item, presentation));
        }

        if (decorations.Count == 0)
        {
            return sorted;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
        ImmutableArray<CompletionItem>.Builder? decorated = null;
        foreach (CompletionDecoration decoration in decorations)
        {
            ImageElement? image = this.imageCatalog.GetOrCreate(decoration.Presentation);
            if (image is null)
            {
                continue;
            }

            decorated ??= sorted.ToBuilder();
            decorated[decoration.Index] = CloneWithImage(decoration.Item, image);
        }

        return decorated?.MoveToImmutable() ?? sorted;
    }

    /// <inheritdoc />
    public Task<FilteredCompletionModel> UpdateCompletionListAsync(
        IAsyncCompletionSession session,
        AsyncCompletionSessionDataSnapshot data,
        CancellationToken token)
        => this.inner.UpdateCompletionListAsync(session, data, token);

    private static List<CompletionSourceGroup> CreateSourceGroups(
        ImmutableArray<CompletionItem> items)
    {
        var groups = new List<CompletionSourceGroup>();
        foreach (CompletionItem item in items)
        {
            CompletionSourceGroup? group = null;
            foreach (CompletionSourceGroup candidate in groups)
            {
                if (ReferenceEquals(candidate.Source, item.Source))
                {
                    group = candidate;
                    break;
                }
            }

            if (group is null)
            {
                group = new CompletionSourceGroup(item.Source);
                groups.Add(group);
            }

            group.Candidates.Add(CreateIdentity(item));
        }

        return groups;
    }

    private static ViuCompletionCandidateIdentity CreateIdentity(CompletionItem item) =>
        new(
            item.DisplayText,
            item.InsertText,
            item.SortText,
            item.FilterText);

    private static CompletionItem CloneWithImage(CompletionItem item, ImageElement image)
    {
        var clone = new CompletionItem(
            item.DisplayText,
            item.Source,
            image,
            item.Filters,
            item.Suffix,
            item.InsertText,
            item.SortText,
            item.FilterText,
            item.AutomationText,
            item.AttributeIcons,
            item.CommitCharacters,
            item.ApplicableToSpan,
            item.IsCommittedAsSnippet,
            item.IsPreselected);

        foreach (KeyValuePair<object, object> property in item.Properties.PropertyList)
        {
            clone.Properties.AddProperty(property.Key, property.Value);
        }

        return clone;
    }

    private sealed class CompletionDecoration
    {
        internal CompletionDecoration(
            int index,
            CompletionItem item,
            ViuCompletionColorPresentation presentation)
        {
            this.Index = index;
            this.Item = item;
            this.Presentation = presentation;
        }

        internal int Index { get; }

        internal CompletionItem Item { get; }

        internal ViuCompletionColorPresentation Presentation { get; }
    }

    private sealed class CompletionSourceGroup
    {
        internal CompletionSourceGroup(IAsyncCompletionSource source) => this.Source = source;

        internal IAsyncCompletionSource Source { get; }

        internal List<ViuCompletionCandidateIdentity> Candidates { get; } = new();
    }
}
