using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// An immutable, host-supplied graph used to resolve CSS-first <c>@reference</c> directives.
/// </summary>
/// <remarks>
/// The graph contains content, not paths to load. This keeps compilation deterministic and I/O-free
/// while allowing an SDK or editor host to apply its own project and package resolution rules.
/// Referenced stylesheets contribute definitions but are never copied to authored CSS output.
/// </remarks>
public sealed class UtilityStylesheetReferenceGraph
{
    private readonly Dictionary<string, UtilityStylesheetReference> referencesByEdge;

    /// <summary>
    /// Gets an empty graph.
    /// </summary>
    public static UtilityStylesheetReferenceGraph Empty { get; } =
        new(Array.Empty<UtilityStylesheetReference>());

    /// <summary>
    /// Creates an immutable graph from already-resolved reference edges.
    /// </summary>
    /// <param name="references">The edges to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="references"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// An identity or specifier is empty, or an edge is registered more than once.
    /// </exception>
    public UtilityStylesheetReferenceGraph(
        IEnumerable<UtilityStylesheetReference> references)
    {
        if (references is null)
        {
            throw new ArgumentNullException(nameof(references));
        }

        var copiedReferences = references
            .OrderBy(reference => reference.ReferencingSourceIdentity, StringComparer.Ordinal)
            .ThenBy(reference => reference.Specifier, StringComparer.Ordinal)
            .ThenBy(reference => reference.ResolvedSourceIdentity, StringComparer.Ordinal)
            .ToArray();
        referencesByEdge = new Dictionary<string, UtilityStylesheetReference>(
            copiedReferences.Length,
            StringComparer.Ordinal);

        foreach (var reference in copiedReferences)
        {
            ValidateReference(reference);
            var key = CreateKey(
                reference.ReferencingSourceIdentity,
                reference.Specifier);
            if (referencesByEdge.ContainsKey(key))
            {
                throw new ArgumentException(
                    $"The reference '{reference.Specifier}' from '{reference.ReferencingSourceIdentity}' is registered more than once.",
                    nameof(references));
            }

            referencesByEdge.Add(key, reference);
        }

        References = copiedReferences.Length == 0
            ? UtilityCollection<UtilityStylesheetReference>.Empty
            : new UtilityCollection<UtilityStylesheetReference>(copiedReferences);
    }

    /// <summary>
    /// Gets the copied edges in deterministic identity and specifier order.
    /// </summary>
    public UtilityCollection<UtilityStylesheetReference> References { get; }

    internal bool TryResolve(
        string referencingSourceIdentity,
        string specifier,
        out UtilityStylesheetReference? reference) =>
        referencesByEdge.TryGetValue(
            CreateKey(
                referencingSourceIdentity,
                specifier),
            out reference);

    private static string CreateKey(
        string referencingSourceIdentity,
        string specifier) =>
        referencingSourceIdentity + "\0" + specifier;

    private static void ValidateReference(UtilityStylesheetReference reference)
    {
        if (reference is null)
        {
            throw new ArgumentException(
                "A stylesheet reference cannot be null.",
                nameof(reference));
        }

        if (string.IsNullOrEmpty(reference.ReferencingSourceIdentity) ||
            string.IsNullOrEmpty(reference.Specifier) ||
            string.IsNullOrEmpty(reference.ResolvedSourceIdentity))
        {
            throw new ArgumentException(
                "Stylesheet reference identities and specifiers cannot be empty.",
                nameof(reference));
        }
    }
}
