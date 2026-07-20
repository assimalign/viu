namespace Assimalign.Viu.Router;

/// <summary>
/// The result of running the guard pipeline for one navigation attempt: a
/// <see cref="NavigationOutcomeKind"/> plus, for a redirect, the requested target. The internal
/// return type of <see cref="Router"/>'s pipeline runner, mapped afterwards onto a confirmed
/// navigation or a <see cref="NavigationFailure"/>.
/// </summary>
internal readonly struct NavigationOutcome
{
    private NavigationOutcome(NavigationOutcomeKind kind, NavigationGuardResult? redirect)
    {
        Kind = kind;
        Redirect = redirect;
    }

    public NavigationOutcomeKind Kind { get; }

    public NavigationGuardResult? Redirect { get; }

    public bool IsAllow => Kind == NavigationOutcomeKind.Allow;

    public static NavigationOutcome Allow { get; } = new(NavigationOutcomeKind.Allow, null);

    public static NavigationOutcome Abort { get; } = new(NavigationOutcomeKind.Abort, null);

    public static NavigationOutcome Cancel { get; } = new(NavigationOutcomeKind.Cancel, null);

    public static NavigationOutcome Redirecting(NavigationGuardResult redirect)
        => new(NavigationOutcomeKind.Redirect, redirect);
}
