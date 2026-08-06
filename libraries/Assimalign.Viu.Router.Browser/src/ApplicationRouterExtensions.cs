using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Router.Browser;

/// <summary>Composes browser-router behavior around a persistent Viu application lifetime.</summary>
public static class ApplicationRouterExtensions
{
    /// <summary>
    /// Installs the router-to-DOM bridge, awaits cancellable initial navigation before mounting, and
    /// removes the bridge after the application unmounts or execution fails.
    /// </summary>
    /// <param name="application">The browser application to decorate.</param>
    /// <param name="router">The borrowed router whose initial navigation must settle before mount.</param>
    /// <returns>The supplied application for further middleware composition.</returns>
    /// <remarks>
    /// The router remains owned by the caller. Registration is ordinary application middleware, so
    /// its cleanup surrounds the full mounted lifetime and runs during cancellation or
    /// <see cref="IApplication.StopAsync(System.Threading.CancellationToken)"/>. Specified by
    /// <c>[APP-4]</c>, <c>[APP-5]</c>, and <c>[RTR-3]</c>.
    /// </remarks>
    public static IApplication UseRouter(
        this IApplication application,
        Router router)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(router);

        return application.Use(async (execution, next) =>
        {
            RouterLinkDomBridge.Install();
            try
            {
                await router.ReadyAsync(execution.Stopping).ConfigureAwait(false);
                await next(execution).ConfigureAwait(false);
            }
            finally
            {
                RouterLinkDomBridge.Uninstall();
            }
        });
    }
}
