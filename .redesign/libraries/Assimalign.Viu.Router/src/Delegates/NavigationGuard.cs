using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// An asynchronous navigation guard. Runs at one of the five pipeline stages — global before-each,
/// per-route before-enter, in-component before-leave and before-update, and global before-resolve —
/// and returns a <see cref="NavigationGuardResult"/> describing whether the navigation may proceed,
/// must abort, or should redirect.
/// </summary>
/// <remarks>
/// <b>Guards decide by return value, never by a continuation callback.</b> An exhaustive result type
/// lets the compiler check that every path decides, and lets the pipeline guarantee a guard decides
/// exactly once — a callback form allows both "never called" and "called twice".
/// <paramref name="cancellationToken"/> is signalled when a later navigation supersedes the one this
/// guard is running for, so a long-running guard can cooperatively bail out of its own asynchronous
/// work; the pipeline additionally re-checks cancellation between guards. Specified by
/// <c>[RTR-5]</c>.
/// </remarks>
/// <param name="to">The resolved location being navigated to.</param>
/// <param name="from">The current location being navigated away from.</param>
/// <param name="cancellationToken">Signalled when this navigation is superseded by a later one.</param>
/// <returns>The guard's decision — <see cref="NavigationGuardResult.Allow"/>, <see cref="NavigationGuardResult.Abort"/>, or a redirect.</returns>
public delegate Task<NavigationGuardResult> NavigationGuard(
    RouteLocation to,
    RouteLocation from,
    CancellationToken cancellationToken);
