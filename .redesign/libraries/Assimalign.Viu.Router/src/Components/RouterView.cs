using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// Renders the matched route subtree at one explicit outlet depth. The outermost view uses depth
/// zero; a nested layout supplies the next depth because Viu deliberately has no hierarchical
/// convention-injection API.
/// </summary>
/// <remarks>
/// The tracked <see cref="Router.CurrentRoute"/> read re-renders the outlet after navigation. A
/// matched <see cref="ComponentNode"/> retains its component reference, slots, listeners,
/// directives, mount reference, and render plan while route arguments override same-named authored
/// arguments. Its effective key combines route-record identity with the authored key: the same
/// matched record is retained across parameter-only navigation, while two records using the same
/// component remain distinct. Non-component route nodes are returned unchanged. One default view
/// is supported per record; named views and lazy route components remain outside this feature.
/// Not thread-safe; Viu drives it on the host event loop. Specified by <c>[RTR-4]</c>,
/// <c>[RTR-7]</c>, <c>[CMP-7]</c>, and <c>[CMP-33]</c>.
/// </remarks>
public sealed class RouterView : IComponent
{
    private static readonly ComponentContract Contract = new(
        renderCacheSize: 0,
        displayName: "RouterView",
        flags: ComponentFlags.None,
        parameters:
        [
            new ComponentParameter("depth", defaultFactory: static () => 0),
        ]);

    /// <summary>Initializes one route-outlet component instance.</summary>
    public RouterView()
    {
    }

    /// <summary>Gets the reflection-free component registration.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(RouterView)),
        Contract,
        static _ => new RouterView());

    /// <inheritdoc/>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Router? router = RouterResolution.Resolve(context);

        return _ =>
        {
            if (router is null)
            {
                return null;
            }

            RouteLocation route = router.CurrentRoute.Value;
            IReadOnlyList<RouteRecord> matched = route.Matched;
            int depth = ReadDepth(context.Bindings.Parameters);
            if (depth < 0 || depth >= matched.Count)
            {
                return null;
            }

            RouteRecord record = matched[depth];
            if (record.Component is not ComponentNode component)
            {
                return record.Component;
            }

            IReadOnlyDictionary<string, object?> arguments = MergeArguments(
                component.Invocation.Arguments,
                record.ArgumentsResolver?.Invoke(route));
            ComponentInvocation invocation = new(
                arguments,
                component.Invocation.Slots,
                component.Invocation.Listeners,
                component.Invocation.Directives,
                component.Invocation.SlotStability);
            return new ComponentNode(
                component.Component,
                invocation,
                new MatchedRouteKey(record, component.Key),
                component.MountReference,
                component.RenderPlan);
        };
    }

    private static int ReadDepth(IReadOnlyDictionary<string, object?> parameters)
    {
        return parameters.TryGetValue("depth", out object? value) && value is int depth
            ? depth
            : 0;
    }

    private static IReadOnlyDictionary<string, object?> MergeArguments(
        IReadOnlyDictionary<string, object?> existing,
        IReadOnlyDictionary<string, object?>? routeArguments)
    {
        if (routeArguments is null || routeArguments.Count == 0)
        {
            return existing;
        }

        Dictionary<string, object?> merged = new(existing, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> argument in routeArguments)
        {
            merged[argument.Key] = argument.Value;
        }

        return merged;
    }

    private sealed class MatchedRouteKey : IEquatable<MatchedRouteKey>
    {
        private readonly RouteRecord _record;
        private readonly object? _componentKey;

        internal MatchedRouteKey(RouteRecord record, object? componentKey)
        {
            _record = record;
            _componentKey = componentKey;
        }

        public bool Equals(MatchedRouteKey? other)
        {
            return other is not null
                && ReferenceEquals(_record, other._record)
                && Equals(_componentKey, other._componentKey);
        }

        public override bool Equals(object? value)
        {
            return value is MatchedRouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_record, _componentKey);
        }
    }
}
