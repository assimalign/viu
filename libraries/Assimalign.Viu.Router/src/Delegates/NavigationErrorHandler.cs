using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// A global navigation error handler, registered through <see cref="Router.OnError"/>. It receives
/// any unexpected exception thrown by a guard, or by the infinite-redirect safeguard, during
/// navigation, along with the target and current locations. A <see cref="NavigationFailure"/>
/// (abort, cancel, duplicate) is <b>not</b> routed here: an expected non-completion is a value
/// returned from <see cref="Router.Push"/>/<see cref="Router.Replace"/>, while this handler exists
/// only for the unexpected — the split keeps a routine aborted navigation from reading as a bug.
/// </summary>
/// <param name="error">The exception thrown during navigation.</param>
/// <param name="to">The location that was being navigated to.</param>
/// <param name="from">The location that was being navigated away from.</param>
public delegate void NavigationErrorHandler(Exception error, RouteLocation to, RouteLocation from);
