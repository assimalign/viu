using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable resolved location: the concrete path, the optional matched route name, the parsed
/// parameters, the parent-to-child matched record chain, and merged metadata. The C# port of the
/// resolved-location shape returned by vue-router's matcher <c>resolve</c>
/// (<c>packages/router/src/matcher/index.ts</c>); see also
/// https://router.vuejs.org/guide/essentials/nested-routes.html for <c>route.matched</c> semantics.
/// </summary>
/// <remarks>
/// Value equality (path, name, parameters, and the matched chain compared by record identity) so a
/// navigation pipeline can detect same-location navigations and snapshot the current route cheaply.
/// A path resolution that matched nothing returns an instance with an empty
/// <see cref="Matched"/> chain rather than throwing.
/// </remarks>
public sealed class RouteLocation : IEquatable<RouteLocation>
{
    internal RouteLocation(
        string path,
        string? name,
        RouteParameters parameters,
        IReadOnlyList<RouteRecord> matched,
        IReadOnlyDictionary<string, object?> meta)
    {
        Path = path;
        Name = name;
        Parameters = parameters;
        Matched = matched;
        Meta = meta;
    }

    /// <summary>The concrete resolved path.</summary>
    public string Path { get; }

    /// <summary>The name of the matched leaf record, or <see langword="null"/> when unnamed or unmatched.</summary>
    public string? Name { get; }

    /// <summary>The parsed route parameters.</summary>
    public RouteParameters Parameters { get; }

    /// <summary>
    /// The matched record chain ordered parent-to-child (upstream <c>route.matched</c>). Empty when
    /// no route matched.
    /// </summary>
    public IReadOnlyList<RouteRecord> Matched { get; }

    /// <summary>The metadata merged across the matched chain (parent first, child overrides).</summary>
    public IReadOnlyDictionary<string, object?> Meta { get; }

    /// <summary>Whether any route matched.</summary>
    public bool IsMatched => Matched.Count > 0;

    /// <summary>The matched leaf record (the deepest matched child), or <see langword="null"/> when unmatched.</summary>
    public RouteRecord? Route => Matched.Count > 0 ? Matched[^1] : null;

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
        if (!string.Equals(Path, other.Path, StringComparison.Ordinal)
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
        hash.Add(Path, StringComparer.Ordinal);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Parameters);
        hash.Add(Matched.Count);
        return hash.ToHashCode();
    }
}
