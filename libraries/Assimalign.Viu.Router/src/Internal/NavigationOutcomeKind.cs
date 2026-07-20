namespace Assimalign.Viu.Router;

/// <summary>
/// How the guard pipeline resolved for one navigation, before it is turned into a
/// <see cref="NavigationFailure"/> or a confirmed navigation. The internal counterpart of the
/// short-circuit outcomes vue-router's <c>navigate()</c> produces.
/// </summary>
internal enum NavigationOutcomeKind
{
    /// <summary>Every guard allowed the navigation; it may be confirmed.</summary>
    Allow,

    /// <summary>A guard aborted the navigation.</summary>
    Abort,

    /// <summary>The navigation was superseded (cancelled) while a guard was running.</summary>
    Cancel,

    /// <summary>A guard requested a redirect; the pipeline must restart against the new target.</summary>
    Redirect,
}
