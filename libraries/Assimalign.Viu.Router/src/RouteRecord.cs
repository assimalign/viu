using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable route definition: a path, an optional name, optional nested children, optional
/// metadata, an eager route subtree or lazy component factory and its argument resolver, and an
/// optional per-record <see cref="BeforeEnter"/> navigation guard. One record type serves as both
/// the declaration and the normalized table entry, so there is no second shape to keep in sync.
/// Redirects and aliases belong to later router features and are intentionally not modeled here.
/// </summary>
/// <remarks>
/// A reference type with identity semantics: the same instance appears in every resolved
/// <see cref="RouteLocation.Matched"/> chain it participates in, so consumers can compare matched
/// records by reference. Child paths that do not start with <c>/</c> are joined onto the parent's
/// path; an empty child path resolves to the parent's path (the empty-path default child).
/// A lazy factory resolves before confirmation and only a successful result is retained; a failed
/// or cancelled attempt leaves the record retryable. Specified by <c>[RTR-1]</c>, <c>[RTR-4]</c>,
/// <c>[RTR-5]</c>, and <c>[V01.01.08.05]</c>.
/// </remarks>
public sealed class RouteRecord
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMeta =
        new Dictionary<string, object?>(0);
    private ComponentNode? _resolvedComponent;

    /// <summary>Creates a route record.</summary>
    /// <param name="path">
    /// The route path. Top-level paths should start with <c>/</c>. A child path without a leading
    /// <c>/</c> is joined onto its parent's path; an empty child path maps to the parent's path.
    /// </param>
    /// <param name="name">An optional unique route name, used for named resolution.</param>
    /// <param name="children">Optional nested child records.</param>
    /// <param name="meta">Optional arbitrary metadata, merged into every resolved location that matches this record.</param>
    /// <param name="component">
    /// The immutable subtree <see cref="RouterView"/> renders for this record, or
    /// <see langword="null"/> when the record is a component-less grouping path. A
    /// <see cref="ComponentNode"/> is copied with route arguments and matched-record identity;
    /// every other node is returned unchanged.
    /// </param>
    /// <param name="argumentsResolver">
    /// Resolves the arguments passed to <paramref name="component"/>: use
    /// <see cref="RouteComponentArguments.FromParameters"/> to forward the route parameters,
    /// <see cref="RouteComponentArguments.FromValues"/> for a fixed argument set, or a hand-written
    /// resolver for anything else. <see langword="null"/> passes no arguments.
    /// </param>
    /// <param name="beforeEnter">
    /// An optional guard run when this record is entered: the pipeline invokes it after the global
    /// before-each and reused-record before-update guards and before any in-component
    /// <see cref="IRouteEnterGuard"/>, only for a navigation in which this record is newly matched.
    /// <see langword="null"/> registers no per-record enter guard.
    /// </param>
    /// <param name="routeEnterGuard">
    /// An optional component-associated enter guard that runs after asynchronous component resolution and
    /// before global before-resolve guards. It is recorded explicitly because component activation
    /// belongs to the renderer and has not occurred when navigation guards run.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="argumentsResolver"/> is supplied for a node other than
    /// <see cref="ComponentNode"/>.
    /// </exception>
    public RouteRecord(
        string path,
        string? name = null,
        IReadOnlyList<RouteRecord>? children = null,
        IReadOnlyDictionary<string, object?>? meta = null,
        VirtualNode? component = null,
        RouteComponentArgumentsResolver? argumentsResolver = null,
        NavigationGuard? beforeEnter = null,
        IRouteEnterGuard? routeEnterGuard = null)
        : this(
            path,
            name,
            children,
            meta,
            component,
            componentFactory: null,
            argumentsResolver,
            beforeEnter,
            routeEnterGuard)
    {
    }

    /// <summary>
    /// Creates a route record whose component request resolves asynchronously.
    /// </summary>
    /// <param name="path">The route path.</param>
    /// <param name="componentFactory">The typed component-request factory awaited during resolve.</param>
    /// <param name="name">An optional unique route name.</param>
    /// <param name="children">Optional nested child records.</param>
    /// <param name="meta">Optional arbitrary route metadata.</param>
    /// <param name="argumentsResolver">Optional route-derived component arguments.</param>
    /// <param name="beforeEnter">An optional guard run before component resolution.</param>
    /// <param name="routeEnterGuard">An optional guard run after component resolution.</param>
    /// <returns>The lazy route record.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> or <paramref name="componentFactory"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static RouteRecord CreateLazy(
        string path,
        RouteComponentFactory componentFactory,
        string? name = null,
        IReadOnlyList<RouteRecord>? children = null,
        IReadOnlyDictionary<string, object?>? meta = null,
        RouteComponentArgumentsResolver? argumentsResolver = null,
        NavigationGuard? beforeEnter = null,
        IRouteEnterGuard? routeEnterGuard = null)
    {
        ArgumentNullException.ThrowIfNull(componentFactory);
        return new RouteRecord(
            path,
            name,
            children,
            meta,
            component: null,
            componentFactory,
            argumentsResolver,
            beforeEnter,
            routeEnterGuard);
    }

    private RouteRecord(
        string path,
        string? name,
        IReadOnlyList<RouteRecord>? children,
        IReadOnlyDictionary<string, object?>? meta,
        VirtualNode? component,
        RouteComponentFactory? componentFactory,
        RouteComponentArgumentsResolver? argumentsResolver,
        NavigationGuard? beforeEnter,
        IRouteEnterGuard? routeEnterGuard)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (argumentsResolver is not null
            && component is not ComponentNode
            && componentFactory is null)
        {
            throw new ArgumentException(
                "Route component arguments require a ComponentNode request.",
                nameof(argumentsResolver));
        }

        Path = path;
        Name = name;
        Children = children is null || children.Count == 0 ? Array.Empty<RouteRecord>() : [.. children];
        Meta = meta ?? EmptyMeta;
        Component = component;
        ComponentFactory = componentFactory;
        ArgumentsResolver = argumentsResolver;
        BeforeEnter = beforeEnter;
        RouteEnterGuard = routeEnterGuard;
    }

    /// <summary>The route path as declared, before parent joining.</summary>
    public string Path { get; }

    /// <summary>The optional unique route name; the key for named resolution.</summary>
    public string? Name { get; }

    /// <summary>The nested child records.</summary>
    public IReadOnlyList<RouteRecord> Children { get; }

    /// <summary>Arbitrary metadata carried by the record and merged into resolved locations.</summary>
    public IReadOnlyDictionary<string, object?> Meta { get; }

    /// <summary>
    /// The component <see cref="RouterView"/> renders when this record is matched at its depth, or
    /// <see langword="null"/> for a component-less grouping path. The matcher ignores this field — it
    /// is consumed only by the view components.
    /// </summary>
    public VirtualNode? Component { get; private set; }

    /// <summary>
    /// The optional asynchronous component-request factory. The router awaits it after per-record
    /// before-enter guards and before component-associated enter guards. Only a successful result is
    /// cached; failure or cancellation leaves the factory eligible for the next navigation.
    /// </summary>
    public RouteComponentFactory? ComponentFactory { get; }

    /// <summary>
    /// Resolves the arguments passed to <see cref="Component"/> from the resolved location, or
    /// <see langword="null"/> to pass none. See <see cref="RouteComponentArguments"/> for the two
    /// declarative forms.
    /// </summary>
    public RouteComponentArgumentsResolver? ArgumentsResolver { get; }

    /// <summary>
    /// The per-record guard run when this record is entered, or <see langword="null"/>. It fires only
    /// for a navigation in which this record is newly matched — a record that stays matched across a
    /// navigation is reused, not re-entered, so its guard does not run again.
    /// </summary>
    public NavigationGuard? BeforeEnter { get; }

    /// <summary>
    /// The explicitly supplied component-associated enter guard, or null. Explicit registration
    /// preserves AOT-safe discovery without activating a component before its route is confirmed.
    /// </summary>
    public IRouteEnterGuard? RouteEnterGuard { get; }

    internal async Task ResolveComponentAsync(CancellationToken cancellationToken)
    {
        RouteComponentFactory? componentFactory = ComponentFactory;
        if (componentFactory is null || _resolvedComponent is not null)
        {
            return;
        }

        ComponentNode resolvedComponent = await componentFactory(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (resolvedComponent is null)
        {
            throw new InvalidOperationException(
                "A lazy route component factory returned null instead of a ComponentNode request.");
        }

        _resolvedComponent = resolvedComponent;
        Component = resolvedComponent;
    }
}
