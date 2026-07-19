using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable route definition: a path, an optional name, optional nested children, and optional
/// metadata. The C# port of vue-router's route record (the <c>RouteRecordRaw</c> input and the
/// normalized <c>RouteRecord</c>; see https://router.vuejs.org and
/// <c>packages/router/src/types/index.ts</c>). Component wiring, redirects, and guards belong to
/// later router features and are intentionally not modeled here.
/// </summary>
/// <remarks>
/// A reference type with identity semantics: the same instance appears in every resolved
/// <see cref="RouteLocation.Matched"/> chain it participates in, so consumers can compare matched
/// records by reference. Child paths that do not start with <c>/</c> are joined onto the parent's
/// path; an empty child path resolves to the parent's path (the empty-path default child).
/// </remarks>
public sealed class RouteRecord
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMeta =
        new Dictionary<string, object?>(0);

    /// <summary>Creates a route record.</summary>
    /// <param name="path">
    /// The route path. Top-level paths should start with <c>/</c>. A child path without a leading
    /// <c>/</c> is joined onto its parent's path; an empty child path maps to the parent's path.
    /// </param>
    /// <param name="name">An optional unique route name, used for named resolution.</param>
    /// <param name="children">Optional nested child records.</param>
    /// <param name="meta">Optional arbitrary metadata (upstream <c>meta</c>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public RouteRecord(
        string path,
        string? name = null,
        IReadOnlyList<RouteRecord>? children = null,
        IReadOnlyDictionary<string, object?>? meta = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        Path = path;
        Name = name;
        Children = children is null || children.Count == 0 ? Array.Empty<RouteRecord>() : [.. children];
        Meta = meta ?? EmptyMeta;
    }

    /// <summary>The route path as declared (before parent joining). Upstream <c>path</c>.</summary>
    public string Path { get; }

    /// <summary>The optional unique route name. Upstream <c>name</c>.</summary>
    public string? Name { get; }

    /// <summary>The nested child records. Upstream <c>children</c>.</summary>
    public IReadOnlyList<RouteRecord> Children { get; }

    /// <summary>Arbitrary metadata carried by the record and merged into resolved locations. Upstream <c>meta</c>.</summary>
    public IReadOnlyDictionary<string, object?> Meta { get; }
}
