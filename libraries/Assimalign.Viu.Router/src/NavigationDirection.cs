namespace Assimalign.Viu.Router;

/// <summary>
/// The direction of a <c>popstate</c>/<c>go</c> navigation relative to the current entry, derived
/// from the signed distance between the leaving and arriving history positions.
/// </summary>
public enum NavigationDirection
{
    /// <summary>The distance could not be determined; the default, so an unseeded state never reads as a direction.</summary>
    Unknown,

    /// <summary>The navigation moved backward in history.</summary>
    Back,

    /// <summary>The navigation moved forward in history.</summary>
    Forward,
}
