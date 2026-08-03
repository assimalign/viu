using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// The navigation anchor. It renders an <c>&lt;a&gt;</c> whose <c>href</c> is resolved through the
/// router (base included), applies the active and exact-active classes by matching its target
/// against the current route, and intercepts an unmodified primary-button click to navigate
/// client-side instead of triggering a page load. A modified, middle- or right-button, or
/// already-prevented click deliberately falls through to the browser, so open-in-new-tab and the
/// context menu keep working.
/// </summary>
/// <remarks>
/// Deliberate scope decisions (see <c>docs/DESIGN.md</c>): the <c>to</c> target is a string path —
/// there is no location-object form; there is no slot-only rendering mode that hands the resolved
/// href to the caller instead of emitting an anchor; and the <c>target="_blank"</c> escape reads the
/// link's own <c>target</c> attribute. Not thread-safe (single-threaded JS event-loop model).
/// </remarks>
public sealed class RouterLink : IComponentTemplate
{
    private static readonly IReadOnlyList<IComponentParameter> DeclaredParameters =
    [
        new ComponentParameter("to", isRequired: true),
        new ComponentParameter("replace", defaultFactory: static () => false),
        new ComponentParameter("activeClass"),
        new ComponentParameter("exactActiveClass"),
    ];

    /// <inheritdoc/>
    public string? Name => "RouterLink";

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters => DeclaredParameters;

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Router? router = RouterResolution.Resolve(context);

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
                // Modifier keys, an already-prevented event, and non-primary buttons all fall through
                // to the browser, so open-in-new-tab and the context menu keep working.
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
            string? to = context.Arguments.Get<string>("to");
            if (string.IsNullOrEmpty(to))
            {
                return;
            }
            // Push/Replace are awaitable, but a click handler is fire-and-forget; observe the returned
            // task so an unexpected guard exception (already routed to Router.OnError) never surfaces
            // as an unobserved task fault.
            Task<NavigationFailure?> navigation = context.Arguments.Get<bool>("replace")
                ? router.Replace(to)
                : router.Push(to);
            ObserveNavigation(navigation);
        }

        return () =>
        {
            IReadOnlyList<IComponent>? children = RenderDefaultSlot(context);
            if (router is null)
            {
                return ComponentTree.Element("a", children: children);
            }

            string to = context.Arguments.Get<string>("to") ?? string.Empty;
            RouteLocation target = router.Resolve(to);
            // Tracked read: the render effect re-runs on navigation so the active classes stay current.
            RouteLocation current = router.CurrentRoute.Value;
            var (isActive, isExactActive) = ComputeActive(current, target);

            string? activeClass =
                context.Arguments.Get<string>("activeClass") ?? router.LinkActiveClass;
            string? exactActiveClass =
                context.Arguments.Get<string>("exactActiveClass") ?? router.LinkExactActiveClass;
            string? classValue = BuildClass(
                isActive ? activeClass : null,
                isExactActive ? exactActiveClass : null);

            List<IComponentAttribute> anchorAttributes =
            [
                new ComponentAttribute("href", router.CreateHref(target)),
            ];
            if (classValue is not null)
            {
                anchorAttributes.Add(new ComponentAttribute("class", classValue));
            }

            anchorAttributes.Add(
                new ComponentAttribute("onClick", (Action<object?>)Navigate));
            return ComponentTree.Element(
                "a",
                new ComponentAttributes(anchorAttributes),
                children);
        };
    }

    private static IReadOnlyList<IComponent>? RenderDefaultSlot(IComponentContext context)
    {
        if (!context.Slots.TryGetValue("default", out ComponentSlot? slot))
        {
            return null;
        }

        IComponent? content = slot(new ComponentArguments());
        return content is null ? null : [content];
    }

    // The active model: the link is active when its target's leaf record appears anywhere in the
    // current route's matched chain (an ancestor-or-self match) and the current parameters include
    // the target's, so a parent link stays highlighted while a child route is showing. Exact-active
    // additionally requires that record to be the current leaf and the two parameter sets to agree.
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

    // Every parameter the target carries is present in the current location with the same value.
    // A target with no parameters is vacuously included, which is what keeps a parent link active.
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

    private static bool IsBlankTarget(IComponentAttributeCollection attributes)
    {
        return attributes.TryGetValue("target", out object? value)
            && value is string target
            && target.Contains("_blank", StringComparison.OrdinalIgnoreCase);
    }

    private static void ObserveNavigation(Task<NavigationFailure?> navigation)
        => navigation.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
