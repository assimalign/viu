using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable resolved location: the concrete path, the optional matched route name, the parsed
/// query, fragment, parameters, the parent-to-child matched record chain, and merged metadata. The matched chain
/// runs outermost-first, so a nested <see cref="RouterView"/> at depth <c>n</c> renders the n-th
/// entry. Specified by <c>[RTR-2]</c> and <c>[RTR-12]</c>.
/// </summary>
/// <remarks>
/// Value equality (full path including raw suffix and empty delimiters, name, parameters, and the matched chain compared by record identity) so a
/// navigation pipeline can detect same-location navigations and snapshot the current route cheaply.
/// A path resolution that matched nothing returns an instance with an empty
/// <see cref="Matched"/> chain rather than throwing.
/// </remarks>
[DebuggerDisplay("Path = {Path,nq}, Name = {Name,nq}, Parameters = {Parameters.Count}, Matched = {Matched.Count}")]
public sealed class RouteLocation : IEquatable<RouteLocation>
{
    private static readonly IReadOnlyList<RouteRecord> EmptyMatched = Array.Empty<RouteRecord>();
    private static readonly IReadOnlyDictionary<string, object?> EmptyMeta =
        new Dictionary<string, object?>(0);

    internal RouteLocation(
        string path,
        string? name,
        RouteParameters parameters,
        IReadOnlyList<RouteRecord> matched,
        IReadOnlyDictionary<string, object?> meta,
        RouteQuery query = default,
        string fragment = "",
        bool hasQuery = false,
        bool hasFragment = false)
    {
        Path = path;
        Name = name;
        Parameters = parameters;
        Matched = matched;
        Meta = meta;
        Query = query;
        Fragment = fragment;
        HasQuery = hasQuery;
        HasFragment = hasFragment;
        FullPath = !hasQuery && !hasFragment
            ? path
            : string.Concat(path, hasQuery ? "?" + query.RawText : string.Empty,
                hasFragment ? "#" + fragment : string.Empty);
    }

    /// <summary>
    /// The initial (start) location a <see cref="Router.CurrentRoute"/> holds before its first
    /// navigation confirms. It has path <c>"/"</c>, no name, no
    /// parameters, query, or fragment, and — the defining trait — an <b>empty</b> <see cref="Matched"/> chain, so it is
    /// never equal to any resolved route and never renders through a <see cref="RouterView"/>. The
    /// router compares against this exact instance by reference to recognize the first navigation,
    /// which runs the full guard pipeline with <c>from</c> set to this sentinel.
    /// </summary>
    public static RouteLocation Start { get; } =
        new("/", name: null, RouteParameters.Empty, EmptyMatched, EmptyMeta);

    /// <summary>The concrete resolved path without query or fragment; only this text is matched. Specified by <c>[RTR-12]</c>.</summary>
    public string Path { get; }

    /// <summary>The base-stripped path with its original query and fragment, including empty delimiters. Cached for hrefs and history writes. Specified by <c>[RTR-12]</c>.</summary>
    public string FullPath { get; }

    /// <summary>The original query text without <c>?</c>, empty when absent or explicitly empty. Specified by <c>[RTR-12]</c>.</summary>
    public string RawQuery => Query.RawText;

    /// <summary>The immutable decoded query with ordinal, boxing-free accessors. Specified by <c>[RTR-12]</c>.</summary>
    public RouteQuery Query { get; }

    /// <summary>The original fragment without the first <c>#</c>; percent escapes remain encoded, and an absent fragment is empty. Specified by <c>[RTR-12]</c>.</summary>
    public string Fragment { get; }

    /// <summary>Whether a query delimiter was present, including an explicitly empty query. Specified by <c>[RTR-12]</c>.</summary>
    public bool HasQuery { get; }

    /// <summary>Whether a fragment delimiter was present, including an explicitly empty fragment. Specified by <c>[RTR-12]</c>.</summary>
    public bool HasFragment { get; }

    /// <summary>The name of the matched leaf record, or <see langword="null"/> when unnamed or unmatched.</summary>
    public string? Name { get; }

    /// <summary>The parsed route parameters.</summary>
    public RouteParameters Parameters { get; }

    /// <summary>
    /// The matched record chain ordered parent-to-child. Empty when
    /// no route matched.
    /// </summary>
    public IReadOnlyList<RouteRecord> Matched { get; }

    /// <summary>The metadata merged across the matched chain (parent first, child overrides).</summary>
    public IReadOnlyDictionary<string, object?> Meta { get; }

    /// <summary>Whether any route matched.</summary>
    public bool IsMatched => Matched.Count > 0;

    /// <summary>The matched leaf record (the deepest matched child), or <see langword="null"/> when unmatched.</summary>
    public RouteRecord? Route => Matched.Count > 0 ? Matched[^1] : null;

    /// <summary>Determines whether two locations have the same value.</summary>
    /// <param name="left">The first location.</param>
    /// <param name="right">The second location.</param>
    /// <returns>True when both values are null or value-equal; otherwise false.</returns>
    public static bool operator ==(RouteLocation? left, RouteLocation? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>Determines whether two locations have different values.</summary>
    /// <param name="left">The first location.</param>
    /// <param name="right">The second location.</param>
    /// <returns>True when exactly one value is null or their values differ; otherwise false.</returns>
    public static bool operator !=(RouteLocation? left, RouteLocation? right) => !(left == right);

    /// <inheritdoc/>
    public bool Equals(RouteLocation? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (!string.Equals(FullPath, other.FullPath, StringComparison.Ordinal)
            || !string.Equals(Name, other.Name, StringComparison.Ordinal)
            || !Parameters.Equals(other.Parameters)
            || Matched.Count != other.Matched.Count)
        {
            return false;
        }
        for (var index = 0; index < Matched.Count; index++)
        {
            if (!ReferenceEquals(Matched[index], other.Matched[index]))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as RouteLocation);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FullPath, StringComparer.Ordinal);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Parameters);
        hash.Add(Matched.Count);
        return hash.ToHashCode();
    }
}
