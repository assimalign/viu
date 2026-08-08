using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The typed destination carried by a redirected <see cref="NavigationGuardResult"/>. Its
/// <see cref="Kind"/> determines whether <see cref="Value"/> is a location or a registered route
/// name; <see cref="Parameters"/> is empty for a location target. Specified by <c>[RTR-5]</c>.
/// </summary>
public sealed class NavigationRedirectTarget
{
    internal NavigationRedirectTarget(
        NavigationRedirectTargetKind kind,
        string value,
        RouteParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(parameters);
        Kind = kind;
        Value = value;
        Parameters = parameters;
    }

    /// <summary>Gets whether this target is a location or a named route.</summary>
    public NavigationRedirectTargetKind Kind { get; }

    /// <summary>
    /// Gets the base-stripped location when <see cref="Kind"/> is
    /// <see cref="NavigationRedirectTargetKind.Location"/>, or the registered route name when it is
    /// <see cref="NavigationRedirectTargetKind.NamedRoute"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the named-route parameters, or <see cref="RouteParameters.Empty"/> for a location target.
    /// </summary>
    public RouteParameters Parameters { get; }
}
