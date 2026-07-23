using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// Thrown when a chain of guard-driven redirects exceeds the safety cap, indicating a redirect loop —
/// the C# port of vue-router's infinite-redirect detection (<c>packages/router/src/router.ts</c>,
/// which warns <c>"Detected a possibly infinite redirection in a navigation guard..."</c> and aborts
/// to avoid a stack overflow;
/// https://router.vuejs.org/guide/advanced/navigation-guards.html#Redirecting). Unlike a
/// <see cref="NavigationFailure"/>, this is a genuine error: it is routed to every
/// <see cref="NavigationErrorHandler"/> and faults the task returned by
/// <see cref="Router.Push"/>/<see cref="Router.Replace"/>.
/// </summary>
/// <remarks>
/// Viu enforces a fixed redirect-depth cap rather than upstream's development-only same-location
/// warning, so the protection is active in every configuration (a deliberate, documented divergence
/// noted in <c>docs/DESIGN.md</c>).
/// </remarks>
public sealed class NavigationRedirectException : Exception
{
    internal NavigationRedirectException(string message, RouteLocation from, RouteLocation to)
        : base(message)
    {
        From = from;
        To = to;
    }

    /// <summary>The location the looping navigation started from.</summary>
    public RouteLocation From { get; }

    /// <summary>The location whose redirect chain exceeded the cap.</summary>
    public RouteLocation To { get; }

    internal static NavigationRedirectException LoopExceeded(RouteLocation from, RouteLocation to, int depth)
        => new(
            $"Detected a possibly infinite redirection in a navigation guard when going from \"{from.Path}\" "
            + $"to \"{to.Path}\" (exceeded {depth} redirects). Aborting to avoid a stack overflow.",
            from,
            to);
}
