using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// A global navigation error handler — the C# port of the callback registered through vue-router's
/// <c>router.onError</c> (<c>packages/router/src/router.ts</c>,
/// https://router.vuejs.org/api/#onError). It receives any unexpected exception thrown by a guard (or
/// the infinite-redirect safeguard) during navigation, along with the target and current locations.
/// Navigation <see cref="NavigationFailure"/>s (abort/cancel/duplicate) are <b>not</b> routed here —
/// those are returned from <see cref="Router.Push"/>/<see cref="Router.Replace"/> instead, mirroring
/// upstream's split between resolved failures and rejected errors.
/// </summary>
/// <param name="error">The exception thrown during navigation.</param>
/// <param name="to">The location that was being navigated to.</param>
/// <param name="from">The location that was being navigated away from.</param>
public delegate void NavigationErrorHandler(Exception error, RouteLocation to, RouteLocation from);
