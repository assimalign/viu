using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser.Router;

/// <summary>Composes browser-router behavior around a persistent Viu application lifetime.</summary>
public static class ApplicationRouterExtensions
{
    /// <param name="application">The persistent browser application to decorate.</param>
    extension(IApplication application)
    {
        /// <summary>
        /// Installs the router-to-DOM bridge, awaits cancellable initial navigation before mounting,
        /// and removes the bridge after the application unmounts or execution fails.
        /// </summary>
        /// <param name="router">
        /// The borrowed router whose initial navigation must settle before mount.
        /// </param>
        /// <returns>The supplied application for further middleware composition.</returns>
        /// <remarks>
        /// The router remains owned by the caller. Registration is ordinary application middleware,
        /// so its cleanup surrounds the full mounted lifetime and runs during cancellation or
        /// <see cref="IApplication.StopAsync(System.Threading.CancellationToken)"/>. Specified by
        /// <c>[APP-4]</c>, <c>[APP-5]</c>, and <c>[RTR-3]</c>.
        /// </remarks>
        public IApplication UseRouter(global::Assimalign.Viu.Router.Router router)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(router);

            return application.Use(async (context, next) =>
            {
                RouterLinkDomBridge.Install();
                try
                {
                    await router.ReadyAsync(context.Stopping).ConfigureAwait(false);
                    await next(context).ConfigureAwait(false);
                }
                finally
                {
                    RouterLinkDomBridge.Uninstall();
                }
            });
        }
    }
}
