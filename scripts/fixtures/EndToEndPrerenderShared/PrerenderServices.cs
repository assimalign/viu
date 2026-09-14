using System;

using ViuRouter = Assimalign.Viu.Router.Router;

#if STATIC_PRERENDER_HOST
namespace EndToEndPrerenderHost;
#else
namespace EndToEndPrerenderApp;
#endif

internal sealed class PrerenderServices(ViuRouter router) : IServiceProvider
{
    public object? GetService(Type serviceType) => serviceType == typeof(ViuRouter) ? router : null;
}
