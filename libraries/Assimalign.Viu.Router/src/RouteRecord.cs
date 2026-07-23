using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable route definition: a path, an optional name, optional nested children, optional
/// metadata, the component (with its props resolver) the route renders, and an optional per-record
/// <see cref="BeforeEnter"/> navigation guard. The C# port of vue-router's route record (the
/// <c>RouteRecordRaw</c> input and the normalized <c>RouteRecord</c>; see https://router.vuejs.org and
/// <c>packages/router/src/types/index.ts</c>). Redirects and aliases belong to later router features
/// and are intentionally not modeled here.
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
    /// <param name="component">
    /// The component <see cref="RouterView"/> renders for this record (upstream <c>component</c> /
    /// <c>components.default</c>), or <see langword="null"/> when the record is a component-less
    /// grouping path.
    /// </param>
    /// <param name="argumentsResolver">
    /// Resolves the arguments passed to <paramref name="component"/> (upstream <c>props</c>): use
    /// <see cref="RouteComponentArguments.FromParameters"/> for the <c>props: true</c> form,
    /// <see cref="RouteComponentArguments.FromValues"/> for static props, or a hand-written resolver
    /// for the function form. <see langword="null"/> passes no props.
    /// </param>
    /// <param name="beforeEnter">
    /// An optional guard run when this record is entered (upstream <c>beforeEnter</c>): the pipeline
    /// invokes it, after the global <c>beforeEach</c> and reused-record <c>beforeRouteUpdate</c> guards
    /// and before any in-component <see cref="IRouteEnterGuard"/>, only for a navigation in which this
    /// record is newly matched. <see langword="null"/> registers no per-record enter guard.
    /// </param>
    /// <param name="routeEnterGuard">
    /// An optional component-associated enter guard that runs after async component resolution and
    /// before global before-resolve guards. It is recorded explicitly because component activation
    /// belongs to Core and has not occurred when navigation guards run.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="argumentsResolver"/> is supplied for a non-template component.
    /// </exception>
    public RouteRecord(
        string path,
        string? name = null,
        IReadOnlyList<RouteRecord>? children = null,
        IReadOnlyDictionary<string, object?>? meta = null,
        IComponent? component = null,
        RouteComponentArgumentsResolver? argumentsResolver = null,
        NavigationGuard? beforeEnter = null,
        IRouteEnterGuard? routeEnterGuard = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (argumentsResolver is not null
            && component is not ITemplateComponent)
        {
            throw new ArgumentException(
                "Route component arguments require an ITemplateComponent request.",
                nameof(argumentsResolver));
        }

        Path = path;
        Name = name;
        Children = children is null || children.Count == 0 ? Array.Empty<RouteRecord>() : [.. children];
        Meta = meta ?? EmptyMeta;
        Component = component;
        ArgumentsResolver = argumentsResolver;
        BeforeEnter = beforeEnter;
        RouteEnterGuard = routeEnterGuard;
    }

    /// <summary>The route path as declared (before parent joining). Upstream <c>path</c>.</summary>
    public string Path { get; }

    /// <summary>The optional unique route name. Upstream <c>name</c>.</summary>
    public string? Name { get; }

    /// <summary>The nested child records. Upstream <c>children</c>.</summary>
    public IReadOnlyList<RouteRecord> Children { get; }

    /// <summary>Arbitrary metadata carried by the record and merged into resolved locations. Upstream <c>meta</c>.</summary>
    public IReadOnlyDictionary<string, object?> Meta { get; }

    /// <summary>
    /// The component <see cref="RouterView"/> renders when this record is matched at its depth
    /// (upstream <c>component</c>), or <see langword="null"/> for a component-less grouping path. The
    /// matcher ignores this field — it is consumed only by the view components.
    /// </summary>
    public IComponent? Component { get; }

    /// <summary>
    /// Resolves the props passed to <see cref="Component"/> from the resolved location (upstream
    /// <c>props</c>), or <see langword="null"/> to pass none. See
    /// <see cref="RouteComponentArguments"/> for the <c>props: true</c> and static-object forms.
    /// </summary>
    public RouteComponentArgumentsResolver? ArgumentsResolver { get; }

    /// <summary>
    /// The per-record guard run when this record is entered (upstream <c>beforeEnter</c>,
    /// https://router.vuejs.org/guide/advanced/navigation-guards.html#Per-Route-Guard), or
    /// <see langword="null"/>. It fires only for a navigation in which this record is newly matched
    /// (not reused), consistent with vue-router.
    /// </summary>
    public NavigationGuard? BeforeEnter { get; }

    /// <summary>
    /// The explicitly supplied component-associated enter guard, or null. Explicit registration
    /// preserves AOT-safe discovery without activating a component before its route is confirmed.
    /// </summary>
    public IRouteEnterGuard? RouteEnterGuard { get; }
}
