using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// The route outlet — the C# port of vue-router's <c>&lt;RouterView&gt;</c>
/// (<c>packages/router/src/RouterView.ts</c>, https://router.vuejs.org/api/#Component-RouterView).
/// It renders the component of the matched record at its nesting depth: the outermost view renders
/// <c>route.matched[0]</c>, a view nested inside that component renders <c>route.matched[1]</c>, and
/// so on. Because the unified component design has no hierarchical dependency facility, nesting
/// depth is an explicit <c>depth</c> argument (zero by default). The reactive
/// <see cref="Router.CurrentRoute"/> read in the render function re-renders the view on navigation,
/// and the renderer's component diff retains an unchanged matched template request.
/// </summary>
/// <remarks>
/// Deliberate simplifications from vue-router (see <c>docs/DESIGN.md</c>): a single default view per
/// record (no named views), the arguments flow through the record's
/// <see cref="RouteRecord.ArgumentsResolver"/>, and a record without a component renders a comment
/// placeholder rather than being skipped in the depth walk. Not thread-safe (single-threaded JS
/// event-loop model).
/// </remarks>
public sealed class RouterView : IComponentTemplate
{
    private static readonly IReadOnlyList<IComponentParameter> DeclaredParameters =
    [
        new ComponentParameter("depth", defaultFactory: static () => 0),
    ];

    /// <inheritdoc/>
    public string? Name => "RouterView";

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters => DeclaredParameters;

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Router? router = RouterResolution.Resolve(context);

        return () =>
        {
            if (router is null)
            {
                return null;
            }

            // Tracked read: the render effect re-runs on every completed navigation.
            RouteLocation route = router.CurrentRoute.Value;
            IReadOnlyList<RouteRecord> matched = route.Matched;
            int depth = context.Arguments.Get<int>("depth");
            if (depth < 0 || depth >= matched.Count)
            {
                return null;
            }

            RouteRecord record = matched[depth];
            if (record.Component is not IComponent component)
            {
                return null;
            }

            if (component is not ITemplateComponent template)
            {
                return component;
            }

            IComponentArguments? routeArguments =
                record.ArgumentsResolver?.Invoke(route);
            IComponentArguments arguments = routeArguments is null
                ? template.Arguments
                : MergeArguments(template.Arguments, routeArguments);
            return CopyTemplateForRecord(template, arguments, record);
        };
    }

    private static IComponentArguments MergeArguments(
        IComponentArguments existing,
        IComponentArguments routeArguments)
    {
        Dictionary<string, object?> merged = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> argument in existing)
        {
            merged[argument.Key] = argument.Value;
        }

        foreach (KeyValuePair<string, object?> argument in routeArguments)
        {
            merged[argument.Key] = argument.Value;
        }

        return new ComponentArguments(merged);
    }

    private static ITemplateComponent CopyTemplateForRecord(
        ITemplateComponent template,
        IComponentArguments arguments,
        RouteRecord record)
    {
        MatchedRouteKey key = new(record, template.Key);
        return template.TemplateType is Type templateType
            ? new TemplateComponent(
                templateType,
                arguments,
                template.Slots,
                key,
                template.Optimization,
                template.Listeners,
                template.Directives)
            : new TemplateComponent(
                template.TemplateName!,
                arguments,
                template.Slots,
                key,
                template.Optimization,
                template.Listeners,
                template.Directives);
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
