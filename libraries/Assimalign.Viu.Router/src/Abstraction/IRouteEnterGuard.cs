using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Implemented by a route component to contribute a <c>beforeRouteEnter</c> guard — the C# port of
/// vue-router's in-component <c>beforeRouteEnter</c> option
/// (https://router.vuejs.org/guide/advanced/navigation-guards.html#In-Component-Guards). The pipeline
/// invokes it for a record that is <b>entering</b> (its component is not yet mounted), so — matching
/// upstream, where <c>beforeRouteEnter</c> has no access to <c>this</c> — the guard is a stateless
/// member of the component definition rather than an instance callback.
/// </summary>
/// <remarks>
/// <b>Discovery is interface-based, never reflective.</b> The pipeline tests
/// <c>record.Component is IRouteEnterGuard</c>, so a trimmer cannot strip the guard and no
/// user-type reflection is involved (issue #73's boundary). The leave and update in-component guards,
/// which do need per-instance state, are registered instead through
/// <see cref="RouterGuards.OnBeforeRouteLeave"/>/<see cref="RouterGuards.OnBeforeRouteUpdate"/>.
/// Upstream's <c>next(vm =&gt; ...)</c> instance callback is intentionally not modelled (the same
/// no-<c>next</c> divergence as the rest of the guard API).
/// </remarks>
public interface IRouteEnterGuard
{
    /// <summary>
    /// Runs before this component's record is entered, after the per-route <c>beforeEnter</c> and
    /// before the global <c>beforeResolve</c> (upstream's documented order).
    /// </summary>
    /// <param name="to">The resolved location being navigated to.</param>
    /// <param name="from">The current location being navigated away from.</param>
    /// <param name="cancellationToken">Signalled when this navigation is superseded by a later one.</param>
    /// <returns>The guard's decision — allow, abort, or redirect.</returns>
    Task<NavigationGuardResult> BeforeRouteEnter(RouteLocation to, RouteLocation from, CancellationToken cancellationToken);
}
