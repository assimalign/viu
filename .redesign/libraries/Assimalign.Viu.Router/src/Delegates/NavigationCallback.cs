namespace Assimalign.Viu.Router;

/// <summary>
/// A listener invoked when the history location changes outside an application-initiated
/// <see cref="IRouterHistory.Push"/>/<see cref="IRouterHistory.Replace"/> — a browser back/forward
/// (<c>popstate</c>) or a memory <see cref="IRouterHistory.Go"/>. The navigation pipeline
/// ([V01.01.08.04]) registers one through <see cref="IRouterHistory.Listen"/> to drive resolution
/// and guards for a navigation the application did not initiate.
/// </summary>
/// <param name="to">The location navigated to (base already stripped).</param>
/// <param name="from">The location navigated from.</param>
/// <param name="information">The navigation type, direction, and signed distance.</param>
/// <remarks>Specified by <c>[RTR-3]</c>.</remarks>
public delegate void NavigationCallback(string to, string from, NavigationInformation information);
