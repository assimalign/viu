using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Executes Viu Utilities CSS-first customization without file access, JavaScript, or Tailwind.
/// </summary>
/// <remarks>
/// Functional utilities and composition follow the independent v4.3.3-compatible contract described
/// by the official
/// <see href="https://tailwindcss.com/docs/adding-custom-styles#functional-utilities">
/// functional utility</see>,
/// <see href="https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants">
/// custom variant</see>, and
/// <see href="https://tailwindcss.com/docs/functions-and-directives">directive</see>
/// documentation.
/// </remarks>
public static class UtilityProjectStylesheetCompiler
{
    /// <summary>
    /// Compiles a project stylesheet using built-in services and no references.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="candidateTexts">The discovered candidate set.</param>
    /// <returns>The rewritten stylesheet and generated custom utility rules.</returns>
    public static UtilityProjectStylesheetCompilationResult Compile(
        string? css,
        IEnumerable<string> candidateTexts) =>
        Compile(
            css,
            candidateTexts,
            null,
            CancellationToken.None);

    /// <summary>
    /// Compiles a project stylesheet with explicit immutable services and cancellation.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="candidateTexts">The discovered candidate set.</param>
    /// <param name="options">Optional source, theme, registry, and reference-graph context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>The rewritten stylesheet and generated custom utility rules.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="candidateTexts"/> or an explicitly configured service is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityProjectStylesheetCompilationResult Compile(
        string? css,
        IEnumerable<string> candidateTexts,
        UtilityProjectStylesheetCompilationOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (candidateTexts is null)
        {
            throw new ArgumentNullException(nameof(candidateTexts));
        }

        var effectiveOptions = options ?? new UtilityProjectStylesheetCompilationOptions();
        if (effectiveOptions.Registry is null)
        {
            throw new ArgumentNullException(nameof(options), "The utility registry cannot be null.");
        }

        if (effectiveOptions.Theme is null)
        {
            throw new ArgumentNullException(nameof(options), "The utility theme cannot be null.");
        }

        if (effectiveOptions.ReferenceGraph is null)
        {
            throw new ArgumentNullException(nameof(options), "The reference graph cannot be null.");
        }

        return UtilityProjectStylesheetCompileEngine.Compile(
            css ?? string.Empty,
            candidateTexts,
            effectiveOptions,
            cancellationToken);
    }

    /// <summary>
    /// Resolves one candidate across built-in utilities, the active theme, and root or referenced
    /// project utility and variant definitions. The returned metadata contains the complete
    /// generated declaration rule used by hover and editor resolution.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="candidateText">The complete class candidate.</param>
    /// <param name="options">Optional source, theme, registry, and reference-graph context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>Generated metadata and recoverable diagnostics from every applicable layer.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="candidateText"/> or an explicitly configured service is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityProjectClassResolutionResult Resolve(
        string? css,
        string candidateText,
        UtilityProjectStylesheetCompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (candidateText is null)
        {
            throw new ArgumentNullException(nameof(candidateText));
        }

        var effectiveOptions = options ?? new UtilityProjectStylesheetCompilationOptions();
        var projectCompilation = Compile(
            css,
            new[] { candidateText },
            effectiveOptions,
            cancellationToken);
        var projectMetadata = projectCompilation.Rules.FirstOrDefault();
        if (projectMetadata is not null)
        {
            return new UtilityProjectClassResolutionResult(
                projectMetadata,
                UtilityCollection<UtilityCssDiagnostic>.Empty,
                projectCompilation.DirectiveDiagnostics,
                projectCompilation.Diagnostics);
        }

        var utilityResolution = effectiveOptions.Registry.Resolve(
            candidateText,
            effectiveOptions.Theme,
            cancellationToken);
        return new UtilityProjectClassResolutionResult(
            utilityResolution.Metadata,
            utilityResolution.Diagnostics,
            projectCompilation.DirectiveDiagnostics,
            projectCompilation.Diagnostics);
    }

    /// <summary>
    /// Gets one bounded completion catalog from the same registry, theme, root stylesheet, and
    /// referenced project definitions used by compilation. Variant chains and a configured theme
    /// prefix are composed uniformly for built-in and project-defined utilities.
    /// </summary>
    /// <param name="css">The authored project stylesheet.</param>
    /// <param name="query">The immutable candidate prefix and result budget.</param>
    /// <param name="options">Optional source, theme, registry, and reference-graph context.</param>
    /// <param name="cancellationToken">The build or editor cancellation boundary.</param>
    /// <returns>Matching metadata in compiler order and whether more matches exist.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="query"/> or an explicitly configured service is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="UtilityClassCompletionQuery.MaximumItems"/> is negative.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityClassCompletionResult GetCompletions(
        string? css,
        UtilityClassCompletionQuery query,
        UtilityProjectStylesheetCompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.MaximumItems < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.MaximumItems),
                query.MaximumItems,
                "The completion item budget cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var effectiveOptions = options ?? new UtilityProjectStylesheetCompilationOptions();
        var definitionCompilation = Compile(
            css,
            Array.Empty<string>(),
            effectiveOptions,
            cancellationToken);
        var lookaheadMaximum = query.MaximumItems == int.MaxValue
            ? int.MaxValue
            : query.MaximumItems + 1;
        var lookupQuery = query with
        {
            MaximumItems = lookaheadMaximum,
        };
        var builtInCompletions = effectiveOptions.Registry.GetCompletions(
            lookupQuery,
            effectiveOptions.Theme,
            cancellationToken);
        var metadataByCandidate = new Dictionary<string, UtilityClassMetadata>(
            StringComparer.Ordinal);
        foreach (var metadata in builtInCompletions.Items)
        {
            metadataByCandidate[metadata.CandidateText] = metadata;
        }

        UtilityCompletionPrefix.Split(
            query.Prefix,
            out var variantPrefix,
            out var baseFragment);
        var projectCandidates = new HashSet<string>(StringComparer.Ordinal);
        if (variantPrefix.Length == 0 ||
            UtilityCompletionPrefix.HasConfiguredPrefix(
                variantPrefix,
                effectiveOptions.Theme.Prefix))
        {
            AddProjectUtilityCandidates(
                definitionCompilation.Utilities,
                query.Prefix,
                variantPrefix,
                baseFragment,
                effectiveOptions.Theme.Prefix,
                projectCandidates);

            if (variantPrefix.Length > 0)
            {
                AddVariantCandidates(
                    effectiveOptions.Registry,
                    effectiveOptions.Theme,
                    variantPrefix,
                    baseFragment,
                    lookaheadMaximum,
                    projectCandidates,
                    cancellationToken);
            }
        }

        var projectCompilation = Compile(
            css,
            projectCandidates,
            effectiveOptions,
            cancellationToken);
        foreach (var metadata in projectCompilation.Rules)
        {
            metadataByCandidate[metadata.CandidateText] = metadata;
        }

        var ordered = metadataByCandidate.Values
            .OrderBy(metadata => metadata.SortOrder)
            .ThenBy(metadata => metadata.CandidateText, StringComparer.Ordinal)
            .ToArray();
        var isTruncated = ordered.Length > query.MaximumItems ||
            builtInCompletions.IsTruncated;
        return new UtilityClassCompletionResult(
            ordered.Length == 0 || query.MaximumItems == 0
                ? UtilityCollection<UtilityClassMetadata>.Empty
                : new UtilityCollection<UtilityClassMetadata>(
                    ordered.Take(query.MaximumItems)),
            isTruncated);
    }

    private static void AddProjectUtilityCandidates(
        UtilityCollection<UtilityCustomUtilityDefinition> definitions,
        string? typedPrefix,
        string variantPrefix,
        string baseFragment,
        string? configuredPrefix,
        ISet<string> candidates)
    {
        var effectiveBaseFragment = variantPrefix.Length == 0 &&
            !string.IsNullOrEmpty(configuredPrefix) &&
            configuredPrefix!.StartsWith(
                typedPrefix ?? string.Empty,
                StringComparison.Ordinal)
                    ? string.Empty
                    : baseFragment;
        foreach (var definition in definitions)
        {
            var baseCandidate = definition.IsFunctional
                ? definition.Name.Substring(
                    0,
                    definition.Name.Length - "-*".Length)
                : definition.Name;
            if (!baseCandidate.StartsWith(
                    effectiveBaseFragment,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = UtilityCompletionPrefix.Compose(
                variantPrefix,
                configuredPrefix,
                baseCandidate);
            if (candidate.StartsWith(
                    typedPrefix ?? string.Empty,
                    StringComparison.Ordinal))
            {
                candidates.Add(candidate);
            }
        }
    }

    private static void AddVariantCandidates(
        UtilityCssRegistry registry,
        UtilityTheme theme,
        string variantPrefix,
        string baseFragment,
        int maximumItems,
        ISet<string> candidates,
        CancellationToken cancellationToken)
    {
        foreach (var metadata in registry.GetCompletionItems(theme))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var baseCandidate = UtilityCompletionPrefix.RemoveConfiguredPrefix(
                metadata.CandidateText,
                theme.Prefix);
            if (UtilityCompletionPrefix.HasVariant(baseCandidate) ||
                !baseCandidate.StartsWith(
                    baseFragment,
                    StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(
                UtilityCompletionPrefix.Compose(
                    variantPrefix,
                    theme.Prefix,
                    baseCandidate));
            if (candidates.Count >= maximumItems)
            {
                return;
            }
        }
    }
}
