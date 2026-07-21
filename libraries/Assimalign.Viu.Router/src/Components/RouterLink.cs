using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Router;

/// <summary>
/// The navigation anchor — the C# port of vue-router's <c>&lt;RouterLink&gt;</c>
/// (<c>packages/router/src/RouterLink.ts</c>, https://router.vuejs.org/api/#Component-RouterLink).
/// It renders an <c>&lt;a&gt;</c> whose <c>href</c> is resolved through the router (base included),
/// applies the active and exact-active classes by matching its target against the current route
/// (https://router.vuejs.org/guide/essentials/active-links.html), and intercepts an unmodified
/// primary-button click to navigate client-side instead of triggering a page load — a modified,
/// middle/right-button, or already-prevented click falls through to the browser
/// (upstream's <c>guardEvent</c>).
/// </summary>
/// <remarks>
/// Deliberate simplifications from vue-router (see <c>docs/DESIGN.md</c>): a string <c>to</c> target
/// (no location-object form), no <c>custom</c>/slot-only rendering, and the <c>target="_blank"</c>
/// guard reads the link's own <c>target</c> attribute. Not thread-safe (single-threaded JS
/// event-loop model).
/// </remarks>
public sealed class RouterLink : IComponentDefinition
{
    private static readonly IReadOnlyList<ComponentPropertyDefinition> DeclaredProperties =
    [
        new ComponentPropertyDefinition("to") { Required = true },
        new ComponentPropertyDefinition("replace") { DefaultValue = false },
        new ComponentPropertyDefinition("activeClass"),
        new ComponentPropertyDefinition("exactActiveClass"),
    ];

    /// <inheritdoc/>
    public string? Name => "RouterLink";

    /// <inheritdoc/>
    public IReadOnlyList<ComponentPropertyDefinition>? Properties => DeclaredProperties;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        // Resolve the router service-first-then-provide ([V01.01.03.24]).
        var router = RouterResolution.Resolve();

        // Built once and reused, so the anchor's onClick prop is a stable reference across renders
        // (no listener re-patch). Reads to/replace at click time — no active effect, so untracked.
        void Navigate(object? raw)
        {
            if (router is null)
            {
                return;
            }
            if (raw is RouterLinkClickEvent click)
            {
                // guardEvent: modifier keys, an already-prevented event, and non-primary buttons all
                // fall through to the browser (upstream RouterLink.ts).
                if (click.HasSystemModifier || click.DefaultPrevented || click.Button != 0)
                {
                    return;
                }
                // target="_blank" opens a new context — let the browser handle it (before preventDefault).
                if (IsBlankTarget(context.Attributes))
                {
                    return;
                }
                click.PreventDefault();
            }
            var to = properties.Get<string>("to");
            if (string.IsNullOrEmpty(to))
            {
                return;
            }
            // Push/Replace are awaitable, but a click handler is fire-and-forget; observe the returned
            // task so an unexpected guard exception (already routed to Router.OnError) never surfaces
            // as an unobserved task fault.
            var navigation = properties.Get<bool>("replace") ? router.Replace(to) : router.Push(to);
            ObserveNavigation(navigation);
        }

        return () =>
        {
            var children = VirtualNodeFactory.RenderSlot(context.Slots, "default");
            if (router is null)
            {
                return VirtualNodeFactory.Element("a", (VirtualNodeProperties?)null, children);
            }
            var to = properties.Get<string>("to") ?? string.Empty;
            var target = router.Resolve(to);
            // Tracked read: the render effect re-runs on navigation so the active classes stay current.
            var current = router.CurrentRoute.Value;
            var (isActive, isExactActive) = ComputeActive(current, target);

            var activeClass = properties.Get<string>("activeClass") ?? router.LinkActiveClass;
            var exactActiveClass = properties.Get<string>("exactActiveClass") ?? router.LinkExactActiveClass;
            var classValue = BuildClass(
                isActive ? activeClass : null,
                isExactActive ? exactActiveClass : null);

            var anchorProperties = new VirtualNodeProperties(3);
            anchorProperties.Set("href", router.CreateHref(target));
            if (classValue is not null)
            {
                anchorProperties.Set("class", classValue);
            }
            anchorProperties.Set("onClick", (Action<object?>)Navigate);
            return VirtualNodeFactory.Element("a", anchorProperties, children);
        };
    }

    // Upstream RouterLink active model: the link is active when its target's leaf record appears in
    // the current route's matched chain (an ancestor-or-self match) and the current params include the
    // target's; exact-active additionally requires that record to be the current leaf with equal params
    // (isSameRouteLocationParams). https://router.vuejs.org/guide/essentials/active-links.html
    private static (bool IsActive, bool IsExactActive) ComputeActive(RouteLocation current, RouteLocation target)
    {
        if (target.Matched.Count == 0)
        {
            return (false, false);
        }
        var targetLeaf = target.Matched[^1];
        var index = -1;
        for (var position = 0; position < current.Matched.Count; position++)
        {
            if (ReferenceEquals(current.Matched[position], targetLeaf))
            {
                index = position;
                break;
            }
        }
        if (index < 0)
        {
            return (false, false);
        }
        var isActive = IncludesParameters(current.Parameters, target.Parameters);
        var isExactActive = isActive
            && index == current.Matched.Count - 1
            && current.Parameters.Equals(target.Parameters);
        return (isActive, isExactActive);
    }

    // Every parameter the target carries is present in the current location with the same value
    // (upstream includesParams). The target having no params is vacuously included (a parent link).
    private static bool IncludesParameters(RouteParameters current, RouteParameters target)
    {
        foreach (var name in target.Names)
        {
            if (!current.TryGetString(name, out var currentValue)
                || !string.Equals(currentValue, target.GetString(name), StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string? BuildClass(string? active, string? exactActive)
    {
        if (active is null)
        {
            return exactActive;
        }
        return exactActive is null ? active : active + " " + exactActive;
    }

    private static bool IsBlankTarget(ComponentAttributes attributes)
        => attributes["target"] is string target
            && target.Contains("_blank", StringComparison.OrdinalIgnoreCase);

    private static void ObserveNavigation(Task<NavigationFailure?> navigation)
        => navigation.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
