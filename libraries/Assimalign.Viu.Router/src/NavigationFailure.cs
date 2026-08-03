namespace Assimalign.Viu.Router;

/// <summary>
/// A navigation that did not complete. Returned from
/// <see cref="Router.Push"/>/<see cref="Router.Replace"/> and handed to every
/// <see cref="AfterNavigationHook"/>, it records the failure <see cref="Type"/> and the
/// <see cref="To"/>/<see cref="From"/> locations involved. Specified by <c>[RTR-6]</c>.
/// </summary>
/// <remarks>
/// A failure is a plain returned value, never an exception: <see cref="Router.Push"/> completes
/// with it rather than throwing, and only a genuinely unexpected guard exception faults the returned
/// task. An abort, a cancellation, and a duplicate are all ordinary outcomes of a correct
/// application, so keeping them out of exception control flow means a caller that ignores the return
/// value never sees a crash for routine behavior.
/// </remarks>
public sealed class NavigationFailure
{
    internal NavigationFailure(NavigationFailureType type, RouteLocation to, RouteLocation from)
    {
        Type = type;
        To = to;
        From = from;
    }

    /// <summary>The reason the navigation did not complete.</summary>
    public NavigationFailureType Type { get; }

    /// <summary>The location the navigation was heading to.</summary>
    public RouteLocation To { get; }

    /// <summary>The location the navigation started from.</summary>
    public RouteLocation From { get; }

    /// <summary>
    /// Whether this failure is of <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The failure type to test against.</param>
    /// <returns><see langword="true"/> when <see cref="Type"/> equals <paramref name="type"/>.</returns>
    public bool Is(NavigationFailureType type) => Type == type;
}
