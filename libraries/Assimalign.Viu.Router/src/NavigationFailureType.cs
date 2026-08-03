namespace Assimalign.Viu.Router;

/// <summary>
/// Why a navigation did not complete. A navigation fails for exactly one reason, so these are plain
/// enum members and deliberately <b>not</b> bit flags — do not add a combining value. A redirect is
/// not represented here at all: it re-enters the pipeline, and the outcome reported to the caller is
/// that of the final navigation.
/// </summary>
public enum NavigationFailureType
{
    /// <summary>
    /// A guard returned <see cref="NavigationGuardResult.Abort"/>, so the navigation stopped and the
    /// current route was left untouched.
    /// </summary>
    Aborted,

    /// <summary>
    /// A newer navigation superseded this one before it completed.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The target location was already the current one, so the pipeline was skipped entirely and no
    /// guard ran.
    /// </summary>
    Duplicated,
}
