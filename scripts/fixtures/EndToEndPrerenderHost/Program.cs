using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.ServerRenderer;
using Assimalign.Viu.State;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace EndToEndPrerenderHost;

internal static class Program
{
    internal static Task<int> Main(string[] arguments) => StaticSiteGeneratorHost.RunAsync(
        arguments,
        static () => new StaticSiteGenerator(PrerenderFixture.CreateRoot, new RequestScopeFactory()));

    private sealed class RequestScopeFactory : IServerRenderRequestScopeFactory<StaticSiteRouteContext>
    {
        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<StaticSiteRouteContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ViuRouter router = PrerenderFixture.CreateRouter(request.RequestContext.History);
            IStateStoreRegistry state = StateStores.CreateRegistry();
            PrerenderFixture.Store.Use(state).State.Message = $"server:{request.RequestContext.Route}";
            ServerRenderApplication application = ServerRenderApplication.CreateBuilder(
                request.RootComponent,
                PrerenderFixture.CreateComponents(),
                new PrerenderServices(router))
                .ConfigureApplication(options => options.State = state)
                .Build();
            return ValueTask.FromResult<IServerRenderRequestScope>(new RequestScope(application, router, state));
        }
    }

    private sealed class RequestScope(
        ServerRenderApplication application,
        ViuRouter router,
        IStateStoreRegistry state) : IServerRenderRequestScope
    {
        public ServerRenderApplication Application { get; } = application;

        public SsrContext RenderContext { get; } = new();

        public ValueTask DisposeAsync()
        {
            router.Dispose();
            state.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
