using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// Thrown when a chain of guard-driven redirects exceeds the safety cap, indicating a redirect loop
/// that would otherwise recurse until the stack overflows. Unlike a <see cref="NavigationFailure"/>,
/// this is a genuine error: a loop is always a bug in the route table or its guards, so it is routed
/// to every <see cref="NavigationErrorHandler"/> and faults the task returned by
/// <see cref="Router.PushAsync"/>/<see cref="Router.ReplaceAsync"/>. Specified by <c>[RTR-6]</c>.
/// </summary>
/// <remarks>
/// The depth cap is fixed and active in every configuration, not just development builds: an
/// unbounded redirect chain fails as a stack overflow in production, which is far harder to diagnose
/// than a typed exception (see <c>docs/DESIGN.md</c>).
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
