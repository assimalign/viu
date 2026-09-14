using Assimalign.Viu.Router;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Carries the route and owned memory history into an application's request factory.</summary>
/// <remarks>The scope must build its Router over this history and expose it through application services. The scope disposes the Router; the generator then disposes the history. Not thread-safe. Specified by <c>[SSG-1]</c>.</remarks>
public sealed class StaticSiteRouteContext
{
    internal StaticSiteRouteContext(string route, string basePath, IRouterHistory history)
    {
        Route = route;
        BasePath = basePath;
        History = history;
    }

    /// <summary>Gets the base-stripped route including its raw query and fragment under <c>[RTR-12]</c>.</summary>
    public string Route { get; }

    /// <summary>Gets the deployment base used for memory-history link generation under <c>[SSG-2]</c>.</summary>
    public string BasePath { get; }

    /// <summary>Gets the borrowed history already positioned at <see cref="Route"/> before composition.</summary>
    public IRouterHistory History { get; }
}
