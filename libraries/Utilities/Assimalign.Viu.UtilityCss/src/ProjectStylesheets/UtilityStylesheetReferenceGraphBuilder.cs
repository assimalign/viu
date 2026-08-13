using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// Builds an immutable project stylesheet reference graph through a host-supplied resolver while
/// keeping the utility compiler itself free of file-system and package-resolution policy.
/// </summary>
public static class UtilityStylesheetReferenceGraphBuilder
{
    /// <summary>
    /// Traverses valid <c>@reference</c> directives from one root stylesheet.
    /// </summary>
    /// <param name="rootSourceIdentity">The stable root stylesheet identity.</param>
    /// <param name="rootCss">The complete root stylesheet content.</param>
    /// <param name="resolver">
    /// Resolves a referencing identity and decoded specifier to a content-bearing edge, or
    /// <see langword="null"/> when the host cannot resolve it. Missing edges remain available to the
    /// project compiler's normal unresolved-reference diagnostic.
    /// </param>
    /// <param name="cancellationToken">The host cancellation boundary.</param>
    /// <returns>The deterministic transitive reference graph.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="rootSourceIdentity"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rootCss"/> or <paramref name="resolver"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public static UtilityStylesheetReferenceGraph Build(
        string rootSourceIdentity,
        string rootCss,
        Func<string, string, UtilityStylesheetReference?> resolver,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rootSourceIdentity))
        {
            throw new ArgumentException(
                "A root stylesheet identity is required.",
                nameof(rootSourceIdentity));
        }

        if (rootCss is null)
        {
            throw new ArgumentNullException(nameof(rootCss));
        }

        if (resolver is null)
        {
            throw new ArgumentNullException(nameof(resolver));
        }

        var references = new List<UtilityStylesheetReference>();
        var visitedSources = new HashSet<string>(StringComparer.Ordinal);
        var visitedEdges = new HashSet<string>(StringComparer.Ordinal);
        Visit(
            rootSourceIdentity,
            rootCss,
            resolver,
            visitedSources,
            visitedEdges,
            references,
            cancellationToken);
        return references.Count == 0
            ? UtilityStylesheetReferenceGraph.Empty
            : new UtilityStylesheetReferenceGraph(references);
    }

    private static void Visit(
        string sourceIdentity,
        string css,
        Func<string, string, UtilityStylesheetReference?> resolver,
        ISet<string> visitedSources,
        ISet<string> visitedEdges,
        ICollection<UtilityStylesheetReference> references,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!visitedSources.Add(sourceIdentity))
        {
            return;
        }

        var parse = UtilityStylesheetParser.Parse(
            css,
            new UtilityStylesheetParseOptions
            {
                SourceIdentity = sourceIdentity,
            },
            cancellationToken);
        foreach (var directive in parse.Directives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var specifier = directive.Identifier;
            if (directive.Kind != UtilityDirectiveKind.Reference ||
                !directive.IsValid ||
                specifier is null ||
                specifier.Length == 0)
            {
                continue;
            }

            var edgeKey =
                sourceIdentity +
                "\0" +
                specifier;
            if (!visitedEdges.Add(edgeKey))
            {
                continue;
            }

            var reference = resolver(
                sourceIdentity,
                specifier);
            if (reference is null)
            {
                continue;
            }

            references.Add(reference);
            Visit(
                reference.ResolvedSourceIdentity,
                reference.Css,
                resolver,
                visitedSources,
                visitedEdges,
                references,
                cancellationToken);
        }
    }
}
