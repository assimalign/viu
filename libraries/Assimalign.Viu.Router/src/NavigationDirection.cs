namespace Assimalign.Viu.Router;

/// <summary>
/// The direction of a <c>popstate</c>/<c>go</c> navigation relative to the current entry, derived
/// from the signed distance between the leaving and arriving history positions. The C# port of
/// vue-router's <c>NavigationDirection</c> (<c>packages/router/src/history/common.ts</c>), whose
/// string values are <c>"back"</c>, <c>"forward"</c>, and <c>""</c> (unknown).
/// </summary>
public enum NavigationDirection
{
    /// <summary>The distance could not be determined (upstream <c>NavigationDirection.unknown</c>, value <c>""</c>).</summary>
    Unknown,

    /// <summary>The navigation moved backward in history (upstream <c>NavigationDirection.back</c>).</summary>
    Back,

    /// <summary>The navigation moved forward in history (upstream <c>NavigationDirection.forward</c>).</summary>
    Forward,
}
