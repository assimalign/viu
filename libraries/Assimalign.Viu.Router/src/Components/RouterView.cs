using System;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Router;

/// <summary>
/// The route outlet — the C# port of vue-router's <c>&lt;RouterView&gt;</c>
/// (<c>packages/router/src/RouterView.ts</c>, https://router.vuejs.org/api/#Component-RouterView).
/// It renders the component of the matched record at its nesting depth: the outermost view renders
/// <c>route.matched[0]</c>, a view nested inside that component renders <c>route.matched[1]</c>, and
/// so on. Depth flows down through provide/inject (<see cref="RouterInjectionKeys.ViewDepth"/>); the
/// reactive <see cref="Router.CurrentRoute"/> read in the render function re-renders the view on
/// navigation, and the renderer's own component diff keeps an unaffected view from re-rendering when
/// only a leaf changed.
/// </summary>
/// <remarks>
/// Deliberate simplifications from vue-router (see <c>docs/DESIGN.md</c>): a single default view per
/// record (no named views), the props flow through the record's
/// <see cref="RouteRecord.PropertiesResolver"/>, and a record without a component renders a comment
/// placeholder rather than being skipped in the depth walk. Not thread-safe (single-threaded JS
/// event-loop model).
/// </remarks>
public sealed class RouterView : IComponentDefinition
{
    /// <inheritdoc/>
    public string? Name => "RouterView";

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        // Inject the router (a missing provide warns through the runtime's inject diagnostics) and
        // this view's depth, then provide depth + 1 for any view nested in the rendered component
        // (upstream: inject(viewDepthKey, 0); provide(viewDepthKey, depth + 1)).
        var router = DependencyInjection.Inject(RouterInjectionKeys.Router);
        var depth = DependencyInjection.Inject(RouterInjectionKeys.ViewDepth, 0);
        DependencyInjection.Provide(RouterInjectionKeys.ViewDepth, depth + 1);

        // Provide a mutable holder for this depth's matched record (upstream: provide(matchedRouteKey,
        // ...)). The render updates it before creating the child vnode, so the child's in-component
        // guard composables read the record they are rendered for even when a reused view swaps leaves.
        var recordScope = new MatchedRecordScope();
        DependencyInjection.Provide(RouterInjectionKeys.MatchedRecord, recordScope);

        return () =>
        {
            if (router is null)
            {
                recordScope.Record = null;
                return null;
            }
            // Tracked read: the render effect re-runs on every completed navigation.
            var route = router.CurrentRoute.Value;
            var matched = route.Matched;
            if (depth >= matched.Count)
            {
                recordScope.Record = null;
                return null;
            }
            var record = matched[depth];
            recordScope.Record = record;
            if (record.Component is not { } component)
            {
                return null;
            }
            var componentProperties = record.PropertiesResolver?.Invoke(route);
            return VirtualNodeFactory.Component(component, componentProperties);
        };
    }
}
