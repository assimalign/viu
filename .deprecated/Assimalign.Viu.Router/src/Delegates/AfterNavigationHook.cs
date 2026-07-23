namespace Assimalign.Viu.Router;

/// <summary>
/// A global after-navigation hook — the C# port of vue-router's <c>afterEach</c> guard
/// (<c>packages/router/src/router.ts</c>, https://router.vuejs.org/guide/advanced/navigation-guards.html#Global-After-Hooks).
/// It runs after every navigation is confirmed (or has failed), cannot change the outcome, and
/// receives the <see cref="NavigationFailure"/> when the navigation did not complete — the C# port of
/// upstream's <c>(to, from, failure) =&gt; {}</c> signature.
/// </summary>
/// <param name="to">The location that was navigated to.</param>
/// <param name="from">The location that was navigated away from.</param>
/// <param name="failure">The failure that aborted, cancelled, or duplicated the navigation, or <see langword="null"/> on success.</param>
public delegate void AfterNavigationHook(RouteLocation to, RouteLocation from, NavigationFailure? failure);
