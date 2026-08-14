using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class UtilityCssProjectContext
{
    internal static UtilityCssProjectContext Default { get; } =
        new(
            string.Empty,
            new UtilityProjectStylesheetCompilationOptions(),
            UtilityVariantRegistry.BuiltIn,
            UtilityCssEditorCatalog.Empty);

    internal UtilityCssProjectContext(
        string entryStylesheetCss,
        UtilityProjectStylesheetCompilationOptions compilationOptions,
        UtilityVariantRegistry variantRegistry,
        UtilityCssEditorCatalog editorCatalog)
    {
        EntryStylesheetCss = entryStylesheetCss;
        CompilationOptions = compilationOptions;
        VariantRegistry = variantRegistry;
        EditorCatalog = editorCatalog;
    }

    internal string EntryStylesheetCss { get; }

    internal UtilityProjectStylesheetCompilationOptions CompilationOptions { get; }

    internal UtilityTheme Theme => CompilationOptions.Theme;

    internal UtilityVariantRegistry VariantRegistry { get; }

    private UtilityCssEditorCatalog EditorCatalog { get; }

    internal UtilityClassCompletionResult GetCompletions(
        string? prefix,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var engineResult = UtilityProjectStylesheetCompiler.GetCompletions(
            EntryStylesheetCss,
            new UtilityClassCompletionQuery
            {
                Prefix = prefix,
                MaximumItems = maximumItems,
            },
            CompilationOptions,
            cancellationToken);
        if (EditorCatalog.Entries.Count == 0)
        {
            return engineResult with
            {
                IsTruncated = engineResult.IsTruncated || EditorCatalog.IsTruncated,
            };
        }

        var selected = new List<UtilityClassMetadata>(maximumItems);
        var selectedCandidateTexts = new HashSet<string>(StringComparer.Ordinal);
        var engineMetadataByCandidate = engineResult.Items.ToDictionary(
            metadata => metadata.CandidateText,
            StringComparer.Ordinal);
        var isTruncated = engineResult.IsTruncated || EditorCatalog.IsTruncated;
        foreach (var entry in EditorCatalog.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.CandidateText.StartsWith(
                    prefix ?? string.Empty,
                    StringComparison.Ordinal) ||
                !selectedCandidateTexts.Add(entry.CandidateText))
            {
                continue;
            }

            if (selected.Count == maximumItems)
            {
                isTruncated = true;
                continue;
            }

            if (!engineMetadataByCandidate.TryGetValue(
                    entry.CandidateText,
                    out var metadata))
            {
                var resolution = Resolve(
                    entry.CandidateText,
                    cancellationToken);
                metadata = resolution.IsSuccess && resolution.Metadata is not null
                    ? resolution.Metadata
                    : CreateCatalogMetadata(entry);
            }

            selected.Add(metadata);
        }

        foreach (var metadata in engineResult.Items)
        {
            if (!selectedCandidateTexts.Add(metadata.CandidateText))
            {
                continue;
            }

            if (selected.Count == maximumItems)
            {
                isTruncated = true;
                continue;
            }

            selected.Add(metadata);
        }

        var ordered = selected.Select(
            (metadata, index) => metadata with
            {
                SortOrder = index,
            });
        return new UtilityClassCompletionResult(
            selected.Count == 0
                ? UtilityCollection<UtilityClassMetadata>.Empty
                : new UtilityCollection<UtilityClassMetadata>(ordered),
            isTruncated);
    }

    internal UtilityProjectClassResolutionResult Resolve(
        string candidateText,
        CancellationToken cancellationToken)
        => UtilityProjectStylesheetCompiler.Resolve(
            EntryStylesheetCss,
            candidateText,
            CompilationOptions,
            cancellationToken);

    internal UtilityCandidateScanOptions CreateScanOptions(
        string sourceIdentity,
        int contentOffset)
        => new()
        {
            SourceIdentity = sourceIdentity,
            ContentOffset = contentOffset,
            Prefix = Theme.Prefix,
            VariantRegistry = VariantRegistry,
        };

    private static UtilityClassMetadata CreateCatalogMetadata(
        UtilityCssEditorCatalogEntry entry) =>
        new(
            entry.CandidateText,
            "Generated project utility CSS.",
            entry.Css,
            entry.Index)
        {
            ColorValue = entry.ColorValue,
        };
}
