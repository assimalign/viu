namespace Assimalign.Viu.Router;

/// <summary>
/// Identifies how a <see cref="NavigationRedirectTarget"/> addresses its destination. The kind is
/// explicit so a caller never infers location-versus-name semantics from a string or null value.
/// Specified by <c>[RTR-5]</c>.
/// </summary>
public enum NavigationRedirectTargetKind
{
    /// <summary>The target value is a base-stripped location.</summary>
    Location,

    /// <summary>The target value is a registered route name with accompanying parameters.</summary>
    NamedRoute,
}
