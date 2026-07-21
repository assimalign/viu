using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Router;
using Assimalign.Viu;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The route table and the root-redirect guard, factored out of bootstrap so both the browser app
/// (web history) and the tests (memory history) build the same routes. Three distinct route groups
/// satisfy #103: the paged story lists, item detail, and user pages.
/// <para>
/// The item and user routes are <b>async components</b> (<see cref="AsyncComponents.DefineAsyncComponent"/>):
/// their loader resolves the already-authored view definition. Lazy route views are the vue-hn shape;
/// Viu has no per-view assembly code-splitting yet ([V01.01.08.05]), so this exercises the
/// async-component runtime-resolution contract and its loading/error UI, not a network download.
/// </para>
/// </summary>
internal static class AppRoutes
{
    private static readonly IComponentDefinition ItemRouteComponent = AsyncComponents.DefineAsyncComponent(
        new AsyncComponentOptions
        {
            Loader = () => Task.FromResult<IComponentDefinition>(ItemView.Instance),
            LoadingComponent = LoadingView.Instance,
            ErrorComponent = ErrorView.Instance,
        });

    private static readonly IComponentDefinition UserRouteComponent = AsyncComponents.DefineAsyncComponent(
        new AsyncComponentOptions
        {
            Loader = () => Task.FromResult<IComponentDefinition>(UserView.Instance),
            LoadingComponent = LoadingView.Instance,
            ErrorComponent = ErrorView.Instance,
        });

    /// <summary>Builds the route table (fresh records each call so history and matcher stay independent).</summary>
    /// <returns>The routes: feed lists, item detail, user page, and a catch-all.</returns>
    public static IReadOnlyList<RouteRecord> Create() =>
    [
        // Story lists: /top, /new/2, /show, /ask, /jobs/3 … (page optional, numeric).
        new RouteRecord(
            "/:feed/:page(\\d+)?",
            name: "feed",
            component: StoriesView.Instance,
            propertiesResolver: RouteComponentProperties.FromParameters()),

        // Item detail with the comment tree.
        new RouteRecord(
            "/item/:id(\\d+)",
            name: "item",
            component: ItemRouteComponent,
            propertiesResolver: RouteComponentProperties.FromParameters()),

        // User profile.
        new RouteRecord(
            "/user/:id",
            name: "user",
            component: UserRouteComponent,
            propertiesResolver: RouteComponentProperties.FromParameters()),

        // Catch-all → 404.
        new RouteRecord(
            "/:pathMatch(.*)*",
            name: "not-found",
            component: NotFoundView.Instance),
    ];

    /// <summary>
    /// Redirects the bare root <c>/</c> to the top feed. <see cref="RouteRecord"/> carries no redirect
    /// field, so — as vue-router does for redirects that depend on the target — this is a global
    /// <c>beforeEach</c> guard.
    /// </summary>
    /// <param name="to">The target location.</param>
    /// <param name="from">The current location.</param>
    /// <param name="cancellationToken">Cancels the guard.</param>
    /// <returns>A redirect to <c>/top</c> at the root, otherwise allow.</returns>
    public static Task<NavigationGuardResult> RedirectRoot(RouteLocation to, RouteLocation from, CancellationToken cancellationToken)
        => Task.FromResult(to.Path is "/" or ""
            ? NavigationGuardResult.RedirectTo("/top")
            : NavigationGuardResult.Allow);
}
