using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The value a <see cref="NavigationGuard"/> returns to decide a navigation: proceed, abort, or
/// redirect. Use the shared <see cref="Allow"/> and <see cref="Abort"/> singletons for the common
/// cases and
/// <see cref="RedirectTo(string)"/>/<see cref="RedirectToName(string, RouteParameters)"/> to send the
/// navigation to a different location. Specified by <c>[RTR-5]</c>.
/// </summary>
/// <remarks>
/// Immutable and safe to cache; the two singletons carry no per-navigation state. A redirect result
/// re-enters the navigation pipeline against the new target — the redirected navigation runs every
/// guard again from the top, under the redirect-depth cap enforced by
/// <see cref="NavigationRedirectException"/>.
/// </remarks>
public sealed class NavigationGuardResult
{
    private NavigationGuardResult(
        NavigationGuardAction action,
        string? redirectLocation,
        string? redirectName,
        RouteParameters? redirectParameters)
    {
        Action = action;
        RedirectLocation = redirectLocation;
        RedirectName = redirectName;
        RedirectParameters = redirectParameters;
    }

    /// <summary>
    /// Allow the navigation to proceed to the next guard and stage. Shared and stateless, so
    /// returning it allocates nothing.
    /// </summary>
    public static NavigationGuardResult Allow { get; } =
        new(NavigationGuardAction.Allow, null, null, null);

    /// <summary>
    /// Abort the navigation, leaving <see cref="Router.CurrentRoute"/> and history untouched and
    /// producing an <see cref="NavigationFailureType.Aborted"/> failure. Shared and stateless, so
    /// returning it allocates nothing.
    /// </summary>
    public static NavigationGuardResult Abort { get; } =
        new(NavigationGuardAction.Abort, null, null, null);

    /// <summary>
    /// Redirect the navigation to the given base-stripped path, restarting the pipeline against it —
    /// every guard runs again from the top, under the redirect-depth cap.
    /// </summary>
    /// <param name="location">The path to redirect to.</param>
    /// <returns>A redirect result carrying <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is <see langword="null"/>.</exception>
    public static NavigationGuardResult RedirectTo(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new NavigationGuardResult(NavigationGuardAction.Redirect, location, null, null);
    }

    /// <summary>
    /// Redirect the navigation to a named route with interpolated parameters, restarting the pipeline
    /// against it — every guard runs again from the top, under the redirect-depth cap.
    /// </summary>
    /// <param name="name">The target route name.</param>
    /// <param name="parameters">The parameters to interpolate into the named route.</param>
    /// <returns>A redirect result carrying the named target.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    public static NavigationGuardResult RedirectToName(string name, RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameters);
        return new NavigationGuardResult(NavigationGuardAction.Redirect, null, name, parameters);
    }

    internal NavigationGuardAction Action { get; }

    internal string? RedirectLocation { get; }

    internal string? RedirectName { get; }

    internal RouteParameters? RedirectParameters { get; }
}
