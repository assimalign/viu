namespace Assimalign.Viu.Router;

/// <summary>
/// Identifies the outcome carried by a <see cref="NavigationGuardResult"/> so callers can branch on
/// the guard decision without comparing singleton instances or inspecting nullable strings.
/// Specified by <c>[RTR-5]</c>.
/// </summary>
public enum NavigationGuardOutcomeKind
{
    /// <summary>The guard allowed the navigation to proceed.</summary>
    Allowed,

    /// <summary>The guard rejected the navigation for the reported failure reason.</summary>
    Failed,

    /// <summary>The guard redirected the navigation to the reported typed target.</summary>
    Redirected,
}
