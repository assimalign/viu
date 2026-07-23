namespace Assimalign.Viu.Router;

/// <summary>
/// A listener invoked when the history location changes outside an application-initiated
/// <see cref="IRouterHistory.Push"/>/<see cref="IRouterHistory.Replace"/> — a browser back/forward
/// (<c>popstate</c>) or a memory <see cref="IRouterHistory.Go"/>. The C# port of vue-router's
/// <c>NavigationCallback</c> (<c>packages/router/src/history/common.ts</c>); the navigation
/// pipeline ([V01.01.08.04]) registers one to drive resolution and guards on browser navigation.
/// </summary>
/// <param name="to">The location navigated to (base already stripped).</param>
/// <param name="from">The location navigated from.</param>
/// <param name="information">The navigation type, direction, and signed distance.</param>
public delegate void NavigationCallback(string to, string from, NavigationInformation information);
