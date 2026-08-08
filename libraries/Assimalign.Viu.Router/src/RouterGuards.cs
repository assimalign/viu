using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// The two in-component navigation guard registrations that need a live component instance. Call
/// them during a route component's <c>Setup</c>, passing its explicit
/// <see cref="ComponentContext"/>, to bind a guard to the record at that outlet depth. The guard
/// runs while the record is <b>leaving</b> or being <b>reused</b> and is removed automatically when
/// the component unmounts.
/// </summary>
/// <remarks>
/// <b>Registration hooks the component lifecycle, not reflection.</b> Each call resolves the router
/// from <see cref="ComponentContext.Services"/>, selects the current matched record at the explicit
/// depth, and registers teardown through <see cref="ComponentLifecycle.OnUnmounted(Action)"/>.
/// There is no hierarchical component-dependency fallback (<c>[CMP-24]</c>), which is why the depth is
/// explicit. The before-enter guard, which has no mounted instance to hook, is supplied explicitly on
/// <see cref="RouteRecord.RouteEnterGuard"/> instead.
/// Specified by <c>[RTR-4]</c> and <c>[RTR-5]</c>.
/// </remarks>
public static class RouterGuards
{
    /// <summary>
    /// Registers a <c>beforeRouteLeave</c> guard for the enclosing route component; it runs (deepest
    /// child first) when the component's record is about to be left.
    /// </summary>
    /// <param name="context">The context passed to the route component's setup method.</param>
    /// <param name="guard">The guard to run when leaving.</param>
    /// <param name="depth">The explicit RouterView depth that rendered the component.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="guard"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is negative.</exception>
    public static void OnBeforeRouteLeave(
        ComponentContext context,
        NavigationGuard guard,
        int depth = 0)
    {
        Register(context, guard, depth, leaving: true);
    }

    /// <summary>
    /// Registers a <c>beforeRouteUpdate</c> guard for the enclosing route component; it runs when the
    /// component's record is reused across a navigation (for example a change of route parameters).
    /// </summary>
    /// <param name="context">The context passed to the route component's setup method.</param>
    /// <param name="guard">The guard to run on reuse.</param>
    /// <param name="depth">The explicit RouterView depth that rendered the component.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="guard"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is negative.</exception>
    public static void OnBeforeRouteUpdate(
        ComponentContext context,
        NavigationGuard guard,
        int depth = 0)
    {
        Register(context, guard, depth, leaving: false);
    }

    private static void Register(
        ComponentContext context,
        NavigationGuard guard,
        int depth,
        bool leaving)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        Router? router = RouterResolution.Resolve(context);
        if (router is null)
        {
            return;
        }

        var matched = router.CurrentRoute.Value.Matched;
        if (depth >= matched.Count)
        {
            return;
        }

        RouteRecord record = matched[depth];
        var remove = leaving
            ? router.RegisterLeaveGuard(record, guard)
            : router.RegisterUpdateGuard(record, guard);
        // Bind removal to the component's teardown so a guard never outlives its instance.
        context.Lifecycle.OnUnmounted(remove);
    }
}
