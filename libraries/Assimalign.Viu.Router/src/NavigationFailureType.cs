namespace Assimalign.Viu.Router;

/// <summary>
/// Why a navigation did not complete — the C# port of vue-router's public
/// <c>NavigationFailureType</c> (<c>packages/router/src/errors.ts</c>,
/// https://router.vuejs.org/guide/advanced/navigation-failures.html). Upstream models these as the
/// bit-flag error codes <c>aborted = 4</c>, <c>cancelled = 8</c>, <c>duplicated = 16</c>; a Viu
/// navigation carries exactly one of them, so they are plain enum members rather than flags. The
/// internal <c>NAVIGATION_GUARD_REDIRECT</c> signal is not a failure here — a redirect re-enters the
/// pipeline and the final navigation's own outcome is returned instead.
/// </summary>
public enum NavigationFailureType
{
    /// <summary>
    /// A guard returned <see cref="NavigationGuardResult.Abort"/>, so the navigation stopped and the
    /// current route was left untouched (upstream <c>NavigationFailureType.aborted</c>).
    /// </summary>
    Aborted,

    /// <summary>
    /// A newer navigation superseded this one before it completed (upstream
    /// <c>NavigationFailureType.cancelled</c>).
    /// </summary>
    Cancelled,

    /// <summary>
    /// The target location was already the current one, so the pipeline was skipped (upstream
    /// <c>NavigationFailureType.duplicated</c>).
    /// </summary>
    Duplicated,
}
