using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves locations against a route table. The public contract of the C# port of vue-router's
/// route matcher (the object returned by <c>createRouterMatcher</c>,
/// <c>packages/router/src/matcher/index.ts</c>). Implemented by <see cref="RouteMatcher"/>. A
/// later router feature (the navigation pipeline) depends on this abstraction rather than the
/// concrete matcher.
/// </summary>
/// <remarks>
/// The matcher is pure: it has no dependency on the DOM, JavaScript interop, or the runtime
/// renderer, and is fully exercisable in a plain .NET test host.
/// </remarks>
public interface IRouteMatcher
{
    /// <summary>Adds a route record (and its children) to the table. Mirrors <c>addRoute</c>.</summary>
    /// <param name="record">The route record to add.</param>
    void AddRoute(RouteRecord record);

    /// <summary>
    /// Resolves a path to a location. Mirrors the path branch of <c>resolve</c>: the highest-ranked
    /// matching route wins. A path that matches nothing yields a location with an empty matched
    /// chain (it does not throw).
    /// </summary>
    /// <param name="path">The path to resolve (path portion only — no query or fragment).</param>
    RouteLocation Resolve(string path);

    /// <summary>Resolves a named route with no parameters.</summary>
    /// <param name="name">The route name.</param>
    /// <exception cref="RouteMatcherException">
    /// No route with that name exists, or a required parameter is missing.
    /// </exception>
    RouteLocation ResolveNamed(string name);

    /// <summary>
    /// Resolves a named route, interpolating <paramref name="parameters"/> into the full path.
    /// Mirrors the name branch of <c>resolve</c>.
    /// </summary>
    /// <param name="name">The route name.</param>
    /// <param name="parameters">The parameter values to interpolate.</param>
    /// <exception cref="RouteMatcherException">
    /// No route with that name exists, a required parameter is missing, or an array was supplied for
    /// a non-repeatable parameter.
    /// </exception>
    RouteLocation ResolveNamed(string name, RouteParameters parameters);

    /// <summary>Whether a route with the given name exists in the table.</summary>
    /// <param name="name">The route name.</param>
    bool HasNamedRoute(string name);

    /// <summary>
    /// The records of every registered matcher, ordered by descending specificity. Mirrors
    /// <c>getRoutes</c>.
    /// </summary>
    IReadOnlyList<RouteRecord> GetRoutes();
}
