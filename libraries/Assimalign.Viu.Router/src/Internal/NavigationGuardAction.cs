namespace Assimalign.Viu.Router;

/// <summary>
/// The three decisions a <see cref="NavigationGuard"/> can express through a
/// <see cref="NavigationGuardResult"/>. The C# modelling of vue-router's guard return values —
/// proceed (<c>return true</c>/<c>next()</c>), abort (<c>return false</c>/<c>next(false)</c>), and
/// redirect (<c>return '/path'</c>/<c>next('/path')</c>) — collapsed into an explicit discriminator
/// so the pipeline never inspects a callback.
/// </summary>
internal enum NavigationGuardAction
{
    /// <summary>Proceed to the next guard / stage (upstream <c>next()</c> or a truthy/void return).</summary>
    Allow,

    /// <summary>Abort the navigation, leaving the current route untouched (upstream <c>next(false)</c>).</summary>
    Abort,

    /// <summary>Restart the pipeline against a redirect target (upstream <c>next(location)</c>).</summary>
    Redirect,
}
