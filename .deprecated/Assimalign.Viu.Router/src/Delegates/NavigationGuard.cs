using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// An asynchronous navigation guard — the C# port of vue-router's navigation guard signature
/// (<c>packages/router/src/types/index.ts</c> <c>NavigationGuard</c>,
/// https://router.vuejs.org/guide/advanced/navigation-guards.html). Runs at one of the pipeline
/// stages (<c>beforeEach</c>, per-route <c>beforeEnter</c>, in-component
/// <c>beforeRouteLeave</c>/<c>beforeRouteUpdate</c>, <c>beforeResolve</c>) and returns a
/// <see cref="NavigationGuardResult"/> describing whether the navigation may proceed, must abort, or
/// should redirect.
/// </summary>
/// <remarks>
/// <b>Deliberate C# divergence from vue-router.</b> Upstream guards receive a <c>next</c> callback
/// (<c>next()</c> / <c>next(false)</c> / <c>next('/path')</c>); Viu instead returns a
/// <see cref="NavigationGuardResult"/>, the return-value form vue-router v4 itself documents as
/// preferred. <paramref name="cancellationToken"/> is signalled when a later navigation supersedes the
/// one this guard is running for, so a long-running guard can cooperatively bail out of its own
/// asynchronous work; the pipeline additionally re-checks cancellation between guards.
/// </remarks>
/// <param name="to">The resolved location being navigated to.</param>
/// <param name="from">The current location being navigated away from.</param>
/// <param name="cancellationToken">Signalled when this navigation is superseded by a later one.</param>
/// <returns>The guard's decision — <see cref="NavigationGuardResult.Allow"/>, <see cref="NavigationGuardResult.Abort"/>, or a redirect.</returns>
public delegate Task<NavigationGuardResult> NavigationGuard(
    RouteLocation to,
    RouteLocation from,
    CancellationToken cancellationToken);
