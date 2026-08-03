using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Implemented by a route component to contribute an in-component before-enter guard. The pipeline
/// invokes it for a record that is <b>entering</b> — its component is not yet mounted, so there is no
/// instance to discover the guard from. The guard is therefore supplied explicitly as
/// <see cref="RouteRecord.RouteEnterGuard"/>, and it never observes component instance state.
/// </summary>
/// <remarks>
/// <b>Registration is interface-based, never reflective.</b> A route record directly references the
/// guard, so a trimmer cannot strip it and no user-type reflection or early component activation is
/// involved (issue #73's boundary). The leave and update in-component guards, which do need
/// per-instance state, are registered instead through
/// <see cref="RouterGuards.OnBeforeRouteLeave"/>/<see cref="RouterGuards.OnBeforeRouteUpdate"/>.
/// There is no post-activation instance callback: every guard in Viu decides by return value rather
/// than by invoking a continuation, so a guard cannot defer work until the component exists.
/// Specified by <c>[RTR-5]</c>.
/// </remarks>
public interface IRouteEnterGuard
{
    /// <summary>
    /// Runs before this component's record is entered, after the per-route
    /// <see cref="RouteRecord.BeforeEnter"/> guard and before the global
    /// <see cref="Router.BeforeResolve"/> stage.
    /// </summary>
    /// <param name="to">The resolved location being navigated to.</param>
    /// <param name="from">The current location being navigated away from.</param>
    /// <param name="cancellationToken">Signalled when this navigation is superseded by a later one.</param>
    /// <returns>The guard's decision — allow, abort, or redirect.</returns>
    Task<NavigationGuardResult> BeforeRouteEnter(RouteLocation to, RouteLocation from, CancellationToken cancellationToken);
}
