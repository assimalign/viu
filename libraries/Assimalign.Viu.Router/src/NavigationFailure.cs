namespace Assimalign.Viu.Router;

/// <summary>
/// A navigation that did not complete — the C# port of vue-router's <c>NavigationFailure</c>
/// (<c>packages/router/src/errors.ts</c>,
/// https://router.vuejs.org/guide/advanced/navigation-failures.html). Returned from
/// <see cref="Router.Push"/>/<see cref="Router.Replace"/> and handed to every
/// <see cref="AfterNavigationHook"/>, it records the failure <see cref="Type"/> and the
/// <see cref="To"/>/<see cref="From"/> locations involved.
/// </summary>
/// <remarks>
/// Unlike upstream — where a failure is an <c>Error</c> that a rejected promise carries — a Viu
/// failure is a plain returned value, so <see cref="Router.Push"/> resolves with it rather than
/// throwing (only genuinely unexpected guard exceptions fault the returned task). This keeps the
/// awaitable <c>push</c>/<c>replace</c> contract free of exception control flow while still
/// distinguishing aborts, cancellations, and duplicates.
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
    /// Whether this failure is of <paramref name="type"/> — the C# counterpart of upstream's
    /// <c>isNavigationFailure(failure, type)</c> test.
    /// </summary>
    /// <param name="type">The failure type to test against.</param>
    /// <returns><see langword="true"/> when <see cref="Type"/> equals <paramref name="type"/>.</returns>
    public bool Is(NavigationFailureType type) => Type == type;
}
