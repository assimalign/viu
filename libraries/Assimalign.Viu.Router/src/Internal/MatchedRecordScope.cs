namespace Assimalign.Viu.Router;

/// <summary>
/// A mutable holder a <see cref="RouterView"/> provides to the component it renders, carrying the
/// matched <see cref="RouteRecord"/> for that view's depth. The C# realisation of vue-router's
/// <c>matchedRouteKey</c> provide (<c>packages/router/src/RouterView.ts</c>): it lets the
/// in-component guard composables (<see cref="RouterGuards.OnBeforeRouteLeave"/>/
/// <see cref="RouterGuards.OnBeforeRouteUpdate"/>) discover, at registration time, which record they
/// belong to.
/// </summary>
/// <remarks>
/// The view updates <see cref="Record"/> in its render function before creating the child vnode, so a
/// component reading it during its own <c>Setup</c> — which runs while that vnode mounts — always sees
/// the record it is being rendered for, even when a reused view swaps to a different leaf. A plain
/// field read, so it never establishes a reactive dependency.
/// </remarks>
internal sealed class MatchedRecordScope
{
    /// <summary>The matched record for the providing view's depth, or <see langword="null"/> when none.</summary>
    public RouteRecord? Record { get; set; }
}
