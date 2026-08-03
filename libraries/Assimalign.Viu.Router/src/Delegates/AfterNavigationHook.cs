namespace Assimalign.Viu.Router;

/// <summary>
/// A global after-navigation hook, registered through <see cref="Router.AfterEach"/>. It runs after
/// every navigation has settled — confirmed or failed — and cannot change the outcome: it returns
/// nothing, so the pipeline has already committed by the time it is invoked. It receives the
/// <see cref="NavigationFailure"/> when the navigation did not complete, and <see langword="null"/>
/// when it did.
/// </summary>
/// <param name="to">The location that was navigated to.</param>
/// <param name="from">The location that was navigated away from.</param>
/// <param name="failure">The failure that aborted, cancelled, or duplicated the navigation, or <see langword="null"/> on success.</param>
public delegate void AfterNavigationHook(RouteLocation to, RouteLocation from, NavigationFailure? failure);
