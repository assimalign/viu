using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Router;

/// <summary>
/// The in-component navigation guard composables — the C# port of vue-router's
/// <c>onBeforeRouteLeave</c> and <c>onBeforeRouteUpdate</c>
/// (<c>packages/router/src/navigationGuards.ts</c>,
/// https://router.vuejs.org/guide/advanced/navigation-guards.html#In-Component-Guards). Call them
/// during a route component's <c>Setup</c> to register a guard bound to that component's matched
/// record; the guard runs while the record is <b>leaving</b> or being <b>reused</b> and is removed
/// automatically when the component unmounts.
/// </summary>
/// <remarks>
/// <b>Registration hooks the runtime's component lifecycle, not reflection.</b> Each call injects the
/// record the enclosing <see cref="RouterView"/> is rendering (its provided
/// <see cref="MatchedRecordScope"/>) and registers a teardown through
/// <see cref="Lifecycle.OnUnmounted"/>, so guard discovery is registration-based and trimming-safe
/// (issue #73's boundary). The <c>beforeRouteEnter</c> guard, which has no instance, is contributed by
/// implementing <see cref="IRouteEnterGuard"/> instead. Calling these outside a router-provided route
/// component is a no-op.
/// </remarks>
public static class RouterGuards
{
    /// <summary>
    /// Registers a <c>beforeRouteLeave</c> guard for the enclosing route component; it runs (deepest
    /// child first) when the component's record is about to be left.
    /// </summary>
    /// <param name="guard">The guard to run when leaving.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is <see langword="null"/>.</exception>
    public static void OnBeforeRouteLeave(NavigationGuard guard) => Register(guard, leaving: true);

    /// <summary>
    /// Registers a <c>beforeRouteUpdate</c> guard for the enclosing route component; it runs when the
    /// component's record is reused across a navigation (for example a change of route parameters).
    /// </summary>
    /// <param name="guard">The guard to run on reuse.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guard"/> is <see langword="null"/>.</exception>
    public static void OnBeforeRouteUpdate(NavigationGuard guard) => Register(guard, leaving: false);

    private static void Register(NavigationGuard guard, bool leaving)
    {
        ArgumentNullException.ThrowIfNull(guard);
        var router = DependencyInjection.Inject(RouterInjectionKeys.Router);
        var scope = DependencyInjection.Inject(RouterInjectionKeys.MatchedRecord);
        // No active router / matched record: called outside a router-provided route component
        // (upstream warns in dev; inject's own miss warning stands in, then this is an inert no-op).
        if (router is null || scope?.Record is not { } record)
        {
            return;
        }
        var remove = leaving
            ? router.RegisterLeaveGuard(record, guard)
            : router.RegisterUpdateGuard(record, guard);
        // Bind removal to the component's teardown so a guard never outlives its instance (upstream:
        // registerGuard -> onUnmounted(removeFromList)).
        Lifecycle.OnUnmounted(remove);
    }
}
