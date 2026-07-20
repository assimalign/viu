using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Router;

/// <summary>
/// The router instance the built-in components consume — the minimal C# stand-in for the object
/// vue-router's <c>createRouter</c> returns (<c>packages/router/src/router.ts</c>,
/// https://router.vuejs.org/api/#createRouter). It owns the reactive <see cref="CurrentRoute"/>
/// (a <c>shallowRef</c> over the resolved location, mirroring upstream's <c>currentRoute</c>),
/// resolves targets and hrefs through the matcher and history, and applies application-initiated
/// navigations with <see cref="Push"/>/<see cref="Replace"/>.
/// </summary>
/// <remarks>
/// <para>
/// Navigation here is <b>synchronous</b> — resolve, write history, set the current route — with no
/// guard chain or async resolution. The guarded, cancellable, <c>Task</c>-based navigation pipeline
/// is <c>[V01.01.08.04]</c>; this surface delivers exactly what <see cref="RouterView"/> and
/// <see cref="RouterLink"/> need. Browser back/forward (and memory <c>Go</c>) drive
/// <see cref="CurrentRoute"/> through the history listener registered in the constructor.
/// </para>
/// <para>Not thread-safe: the router targets the single-threaded JS event loop.</para>
/// </remarks>
public sealed class Router : IDisposable
{
    private const string DefaultLinkActiveClass = "router-link-active";
    private const string DefaultLinkExactActiveClass = "router-link-exact-active";

    private readonly IRouteMatcher _matcher;
    private readonly IRouterHistory _history;
    private readonly ShallowReference<RouteLocation> _currentRoute;
    private readonly Action _unlisten;
    private bool _disposed;

    /// <summary>Creates a router over an existing matcher and history.</summary>
    /// <param name="history">The history integration (memory, web, or hash) driving locations.</param>
    /// <param name="matcher">The route table and matcher resolving locations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="history"/> or <paramref name="matcher"/> is null.</exception>
    public Router(IRouterHistory history, IRouteMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(matcher);
        _history = history;
        _matcher = matcher;
        _currentRoute = Reactive.ShallowReference(matcher.Resolve(history.Location));
        _unlisten = history.Listen(OnHistoryNavigation);
    }

    /// <summary>Creates a router over a fresh <see cref="RouteMatcher"/> built from <paramref name="routes"/>.</summary>
    /// <param name="history">The history integration driving locations.</param>
    /// <param name="routes">The top-level route records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="history"/> or <paramref name="routes"/> is null.</exception>
    public Router(IRouterHistory history, IEnumerable<RouteRecord> routes)
        : this(history, new RouteMatcher(routes))
    {
    }

    /// <summary>
    /// The reactive current location (upstream: <c>router.currentRoute</c>, a <c>shallowRef</c>): one
    /// trigger per completed navigation drives every <see cref="RouterView"/> and the active-class
    /// computation of every <see cref="RouterLink"/> through the normal reactivity graph.
    /// </summary>
    public IReference<RouteLocation> CurrentRoute => _currentRoute;

    /// <summary>
    /// The default active class applied to a <see cref="RouterLink"/> whose target is an inclusive
    /// match of the current route (upstream: the <c>linkActiveClass</c> router option). A per-link
    /// <c>activeClass</c> prop overrides it. Defaults to <c>"router-link-active"</c>.
    /// </summary>
    public string LinkActiveClass { get; set; } = DefaultLinkActiveClass;

    /// <summary>
    /// The default exact-active class applied to a <see cref="RouterLink"/> whose target is the exact
    /// current route (upstream: the <c>linkExactActiveClass</c> router option). A per-link
    /// <c>exactActiveClass</c> prop overrides it. Defaults to <c>"router-link-exact-active"</c>.
    /// </summary>
    public string LinkExactActiveClass { get; set; } = DefaultLinkExactActiveClass;

    /// <summary>Resolves a path to a location through the matcher (upstream: <c>router.resolve</c>).</summary>
    /// <param name="location">The base-stripped location to resolve (path portion).</param>
    /// <returns>The resolved location; an unmatched path yields an empty matched chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public RouteLocation Resolve(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _matcher.Resolve(location);
    }

    /// <summary>Resolves a named route with interpolated parameters (upstream: <c>router.resolve({ name, params })</c>).</summary>
    /// <param name="name">The route name.</param>
    /// <param name="parameters">The parameter values to interpolate.</param>
    /// <returns>The resolved location.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="RouteMatcherException">The name is unknown or a required parameter is missing.</exception>
    public RouteLocation ResolveNamed(string name, RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameters);
        return _matcher.ResolveNamed(name, parameters);
    }

    /// <summary>
    /// Builds the anchor <c>href</c> for a location, prefixing the configured base (upstream:
    /// <c>router.resolve(...).href</c> via <c>history.createHref</c>).
    /// </summary>
    /// <param name="location">The base-stripped location.</param>
    /// <returns>The href, base included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public string CreateHref(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _history.CreateHref(location);
    }

    /// <summary>Builds the anchor <c>href</c> for a resolved location's path.</summary>
    /// <param name="location">The resolved location.</param>
    /// <returns>The href, base included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public string CreateHref(RouteLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return _history.CreateHref(location.Path);
    }

    /// <summary>
    /// Navigates to <paramref name="location"/>, pushing a new history entry and updating the reactive
    /// current route (upstream: <c>router.push</c>). Synchronous — no navigation guards
    /// (<c>[V01.01.08.04]</c>).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public void Push(string location) => Navigate(location, replace: false);

    /// <summary>
    /// Navigates to <paramref name="location"/>, replacing the current history entry and updating the
    /// reactive current route (upstream: <c>router.replace</c>). Synchronous — no navigation guards
    /// (<c>[V01.01.08.04]</c>).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public void Replace(string location) => Navigate(location, replace: true);

    /// <summary>Removes the history listener so no back/forward callback outlives the router.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _unlisten();
    }

    private void Navigate(string location, bool replace)
    {
        ArgumentNullException.ThrowIfNull(location);
        var resolved = _matcher.Resolve(location);
        if (replace)
        {
            _history.Replace(location);
        }
        else
        {
            _history.Push(location);
        }
        // shallowRef equality gate: navigating to the identical location is a no-op trigger, so a
        // repeat click never spuriously re-renders (RouteLocation has value equality).
        _currentRoute.Value = resolved;
    }

    // Browser back/forward and memory Go route through the history listener, not Push/Replace, so
    // they re-resolve and drive the reactive current route the same way (upstream: the router's
    // history.listen handler re-resolving to the popped location).
    private void OnHistoryNavigation(string to, string from, NavigationInformation information)
        => _currentRoute.Value = _matcher.Resolve(to);
}
