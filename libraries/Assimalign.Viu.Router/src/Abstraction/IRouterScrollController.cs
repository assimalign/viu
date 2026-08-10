using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Extends a router history that can turn host-free scroll decisions into environment effects after
/// confirmed navigations. A history implementation opts into this capability without placing host
/// behavior on <see cref="IRouterHistory"/> itself. Specified by <c>[RTR-7]</c> and <c>[RTR-9]</c>.
/// </summary>
/// <remarks>
/// The router invokes this contract only after confirmation and never for an aborted, redirected,
/// duplicated, or cancelled navigation. Implementations own host scheduling, selector resolution,
/// effect batching, and cancellation checks; the Router assembly remains host-free.
/// </remarks>
public interface IRouterScrollController
{
    /// <summary>
    /// Observes one confirmed navigation and applies, defers, or invalidates its scroll work. Every
    /// confirmation is reported, including one with no <paramref name="behavior"/>, so a newer
    /// navigation can invalidate deferred work belonging to an older route.
    /// </summary>
    /// <param name="to">The confirmed destination.</param>
    /// <param name="from">The route that was current before confirmation.</param>
    /// <param name="savedPosition">The arriving history entry's saved offset, or <see langword="null"/> for push and replace.</param>
    /// <param name="behavior">The current scroll decision delegate, or <see langword="null"/> when the navigation has no scroll policy.</param>
    /// <param name="isInitialNavigation">
    /// Whether confirmation is the readiness navigation whose effect must remain deferred until
    /// <see cref="CompleteInitialScrollAsync"/> signals that routed content has mounted.
    /// </param>
    /// <param name="cancellationToken">Signals that a newer navigation superseded this work.</param>
    /// <returns>A task representing the effect, or completed deferred/invalidation bookkeeping.</returns>
    Task ApplyAsync(
        RouteLocation to,
        RouteLocation from,
        ScrollPosition? savedPosition,
        ScrollBehavior? behavior,
        bool isInitialNavigation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Signals that the host mounted the initially confirmed route and may now apply its deferred
    /// scroll decision. The operation is idempotent and completes immediately when no current
    /// initial request remains; a newer confirmed navigation must have invalidated any older request.
    /// </summary>
    /// <returns>A task representing the one deferred effect, when present.</returns>
    Task CompleteInitialScrollAsync();
}
