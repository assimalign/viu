using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// The route outlet. It renders the component of the matched record at its nesting depth: the
/// outermost view renders the first entry of <see cref="RouteLocation.Matched"/>, a view nested
/// inside that component renders the second, and so on. Because Viu has no hierarchical component
/// dependency facility, a view cannot discover its own depth from an ancestor — nesting depth is an
/// explicit <c>depth</c> argument (zero by default) that a nested layout passes on. The reactive
/// <see cref="Router.CurrentRoute"/> read in the render function re-renders the view on navigation,
/// and the renderer's component diff retains an unchanged matched template request. Specified by
/// <c>[RTR-4]</c>.
/// </summary>
/// <remarks>
/// Deliberate scope decisions (see <c>docs/DESIGN.md</c>): one default view per record — there are
/// no named views; arguments flow through the record's <see cref="RouteRecord.ArgumentsResolver"/>;
/// and a record without a component renders a comment placeholder rather than being skipped in the
/// depth walk, so a componentless layout record does not shift its children's depth. Not thread-safe
/// (single-threaded JS event-loop model).
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
