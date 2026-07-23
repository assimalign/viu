using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router;

/// <summary>
/// The in-component navigation guard composables — the C# port of vue-router's
/// <c>onBeforeRouteLeave</c> and <c>onBeforeRouteUpdate</c>
/// (<c>packages/router/src/navigationGuards.ts</c>,
/// https://router.vuejs.org/guide/advanced/navigation-guards.html#In-Component-Guards). Call them
/// during a route component's <c>Setup</c>, passing its explicit
/// <see cref="IComponentContext"/>, to bind a guard to the record at that outlet depth. The guard
/// runs while the record is <b>leaving</b> or being <b>reused</b> and is removed automatically when
/// the component unmounts.
/// </summary>
/// <remarks>
/// <b>Registration hooks the component lifecycle, not reflection.</b> Each call resolves the router
/// from <see cref="IComponentContext.Services"/>, selects the current matched record at the explicit
/// depth, and registers teardown through <see cref="IComponentLifecycle.OnUnmounted(Action)"/>.
/// There is no hierarchical component-dependency fallback. The <c>beforeRouteEnter</c> guard, which has
/// no mounted instance, is supplied explicitly on <see cref="RouteRecord"/>.
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
        IComponentContext context,
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
        IComponentContext context,
        NavigationGuard guard,
        int depth = 0)
    {
        Register(context, guard, depth, leaving: false);
    }

    private static void Register(
        IComponentContext context,
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
        // Bind removal to the component's teardown so a guard never outlives its instance (upstream:
        // registerGuard -> onUnmounted(removeFromList)).
        context.Lifecycle.OnUnmounted(remove);
    }
}
