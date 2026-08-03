namespace Assimalign.Viu.Router;

/// <summary>
/// The three decisions a <see cref="NavigationGuard"/> can express through a
/// <see cref="NavigationGuardResult"/>. An explicit discriminator, so the pipeline reads a decision
/// off a value instead of inferring one from a return type or waiting on a continuation.
/// </summary>
internal enum NavigationGuardAction
{
    /// <summary>Proceed to the next guard or stage.</summary>
    Allow,

    /// <summary>Abort the navigation, leaving the current route untouched.</summary>
    Abort,

    /// <summary>Restart the pipeline against a redirect target.</summary>
    Redirect,
}
