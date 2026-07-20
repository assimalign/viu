using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// A route table and matcher: builds compiled, ranked matchers from a set of route records and
/// resolves paths and named routes to immutable <see cref="RouteLocation"/>s. The C# port of the
/// object returned by vue-router's <c>createRouterMatcher</c>
/// (<c>packages/router/src/matcher/index.ts</c>).
/// </summary>
/// <remarks>
/// <para>
/// Matchers are kept sorted by descending specificity, so path resolution returns the highest-ranked
/// match and route-table order never overrides specificity (static beats dynamic beats catch-all).
/// An empty-path child is ordered ahead of its parent so navigating to the parent path resolves the
/// default child — the parent-to-child matched chain of <see cref="RouteLocation.Matched"/>.
/// </para>
/// <para>
/// Pure and DOM-free: no interop, renderer, or reflection. The path patterns compile to interpreted
/// regular expressions (trimming- and NativeAOT-safe). Not thread-safe: build the table before
/// resolving, matching the router's single-threaded (JS event-loop) model.
/// </para>
/// </remarks>
public sealed class RouteMatcher : IRouteMatcher
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMeta =
        new Dictionary<string, object?>(0);

    private readonly List<RouteRecordMatcher> matchers = [];
    private readonly Dictionary<string, RouteRecordMatcher> namedMatchers = new(StringComparer.Ordinal);
    private readonly PathMatchingOptions options;

    /// <summary>Creates an empty matcher with default options.</summary>
    public RouteMatcher()
        : this(Array.Empty<RouteRecord>(), options: null)
    {
    }

    /// <summary>Creates a matcher from a set of route records with default options.</summary>
    /// <param name="routes">The top-level route records.</param>
    public RouteMatcher(IEnumerable<RouteRecord> routes)
        : this(routes, options: null)
    {
    }

    /// <summary>Creates a matcher from a set of route records.</summary>
    /// <param name="routes">The top-level route records.</param>
    /// <param name="options">Matching options, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="routes"/> is <see langword="null"/>.</exception>
    /// <exception cref="RouteMatcherException">A route path or custom parameter pattern is invalid.</exception>
    public RouteMatcher(IEnumerable<RouteRecord> routes, PathMatchingOptions? options)
    {
        ArgumentNullException.ThrowIfNull(routes);
        this.options = options ?? PathMatchingOptions.Default;
        foreach (var route in routes)
        {
            AddRoute(route);
        }
    }

    /// <inheritdoc/>
    public void AddRoute(RouteRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        AddRecord(record, parent: null);
    }

    /// <inheritdoc/>
    public RouteLocation Resolve(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var candidate in matchers)
        {
            if (candidate.Parser.TryParse(path, out var parameters))
            {
                return BuildLocation(candidate, path, parameters);
            }
        }
        return new RouteLocation(path, name: null, RouteParameters.Empty, Array.Empty<RouteRecord>(), EmptyMeta);
    }

    /// <inheritdoc/>
    public RouteLocation ResolveNamed(string name)
        => ResolveNamed(name, RouteParameters.Empty);

    /// <inheritdoc/>
    public RouteLocation ResolveNamed(string name, RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!namedMatchers.TryGetValue(name, out var matcher))
        {
            throw new RouteMatcherException(
                RouteMatcherError.NamedRouteNotFound,
                $"Route with name \"{name}\" does not exist.");
        }

        // stringify enforces required/repeatable parameters and throws a descriptive error.
        var path = matcher.Parser.Stringify(parameters);
        var resolvedParameters = ProjectParameters(matcher, parameters);
        var matched = BuildMatchedChain(matcher);
        var meta = BuildMeta(matched);
        return new RouteLocation(path, matcher.Record.Name, resolvedParameters, matched, meta);
    }

    /// <inheritdoc/>
    public bool HasNamedRoute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return namedMatchers.ContainsKey(name);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RouteRecord> GetRoutes()
    {
        var records = new RouteRecord[matchers.Count];
        for (var index = 0; index < matchers.Count; index++)
        {
            records[index] = matchers[index].Record;
        }
        return records;
    }

    // Test seam: the ranked matcher table.
    internal IReadOnlyList<RouteRecordMatcher> Matchers => matchers;

    private void AddRecord(RouteRecord record, RouteRecordMatcher? parent)
    {
        var normalizedPath = NormalizePath(record.Path, parent);
        var tokens = PathTokenizer.Tokenize(normalizedPath);
        var parser = PathParserFactory.Compile(tokens, options);
        var matcher = new RouteRecordMatcher(record, normalizedPath, parser, parent);

        parent?.Children.Add(matcher);
        if (record.Name is { Length: > 0 } name)
        {
            // Last registration wins, mirroring vue-router's map assignment.
            namedMatchers[name] = matcher;
        }
        InsertMatcher(matcher);

        foreach (var child in record.Children)
        {
            AddRecord(child, matcher);
        }
    }

    // Mirrors vue-router's parent-path joining for non-absolute child paths.
    private static string NormalizePath(string path, RouteRecordMatcher? parent)
    {
        if (parent is null)
        {
            return path;
        }
        if (path.Length > 0 && path[0] == '/')
        {
            return path;
        }
        var parentPath = parent.NormalizedPath;
        if (path.Length == 0)
        {
            return parentPath;
        }
        var connector = parentPath.Length > 0 && parentPath[^1] == '/' ? string.Empty : "/";
        return parentPath + connector + path;
    }

    // Mirrors insertMatcher/findInsertionIndex: binary search by score, then place an empty-path
    // child ahead of an equally scored matchable ancestor so the child wins.
    private void InsertMatcher(RouteRecordMatcher matcher)
    {
        var lower = 0;
        var upper = matchers.Count;
        while (lower != upper)
        {
            var mid = (lower + upper) >> 1;
            var order = PathParserScoreComparer.CompareScore(matcher.Parser.Score, matchers[mid].Parser.Score);
            if (order < 0)
            {
                upper = mid;
            }
            else
            {
                lower = mid + 1;
            }
        }

        var ancestor = GetInsertionAncestor(matcher);
        if (ancestor is not null && upper > 0)
        {
            var ancestorIndex = matchers.LastIndexOf(ancestor, upper - 1);
            if (ancestorIndex >= 0)
            {
                upper = ancestorIndex;
            }
        }

        matchers.Insert(upper, matcher);
    }

    private static RouteRecordMatcher? GetInsertionAncestor(RouteRecordMatcher matcher)
    {
        var ancestor = matcher.Parent;
        while (ancestor is not null)
        {
            if (IsMatchable(ancestor)
                && PathParserScoreComparer.CompareScore(matcher.Parser.Score, ancestor.Parser.Score) == 0)
            {
                return ancestor;
            }
            ancestor = ancestor.Parent;
        }
        return null;
    }

    // Upstream gates on name/components/redirect. Components and redirects are later router
    // features (see DESIGN.md), so every record is currently matchable — which is what keeps an
    // empty-path default child ordered ahead of its parent.
    private static bool IsMatchable(RouteRecordMatcher matcher)
        => matcher is not null;

    private static RouteLocation BuildLocation(RouteRecordMatcher matcher, string path, RouteParameters parameters)
    {
        var matched = BuildMatchedChain(matcher);
        var meta = BuildMeta(matched);
        return new RouteLocation(path, matcher.Record.Name, parameters, matched, meta);
    }

    private static IReadOnlyList<RouteRecord> BuildMatchedChain(RouteRecordMatcher matcher)
    {
        var chain = new List<RouteRecord>();
        var current = matcher;
        while (current is not null)
        {
            chain.Insert(0, current.Record);
            current = current.Parent;
        }
        return chain;
    }

    private static IReadOnlyDictionary<string, object?> BuildMeta(IReadOnlyList<RouteRecord> matched)
    {
        if (matched.Count == 0)
        {
            return EmptyMeta;
        }
        var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var record in matched)
        {
            foreach (var (key, value) in record.Meta)
            {
                meta[key] = value;
            }
        }
        return meta;
    }

    // The leaf matcher's keys span the whole path, so they are the full parameter key set; keep only
    // those (dropping any extraneous provided params) for the resolved location.
    private static RouteParameters ProjectParameters(RouteRecordMatcher matcher, RouteParameters provided)
    {
        var keys = matcher.Parser.Keys;
        if (keys.Length == 0)
        {
            return RouteParameters.Empty;
        }
        var projected = new Dictionary<string, RouteParameterValue>(keys.Length, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (provided.TryGetRawValue(key.Name, out var value))
            {
                projected[key.Name] = value;
            }
        }
        return RouteParameters.FromValues(projected);
    }
}
