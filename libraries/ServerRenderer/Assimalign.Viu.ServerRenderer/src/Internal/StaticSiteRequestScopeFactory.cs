using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Router;

namespace Assimalign.Viu.ServerRenderer;

internal sealed class StaticSiteRequestScopeFactory(
    IServerRenderRequestScopeFactory<StaticSiteRouteContext> factory)
    : IServerRenderRequestScopeFactory<StaticSiteRouteContext>
{
    public async ValueTask<IServerRenderRequestScope> CreateAsync(
        ServerRenderRequest<StaticSiteRouteContext> request,
        CancellationToken cancellationToken = default)
    {
        IServerRenderRequestScope scope = await factory.CreateAsync(request, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The static request factory returned null.");
        try
        {
            if (scope.Application.Context.Services?.GetService(typeof(Router.Router)) is Router.Router router)
            {
                NavigationFailure? failure = await router.ReadyAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (failure is not null)
                {
                    throw new InvalidOperationException($"Route navigation failed: {failure.Type}.");
                }
            }

            return scope;
        }
        catch (Exception failure)
        {
            // Ownership has not yet passed to the adaptor when readiness fails [SSG-1].
            // Consume identities before teardown so a later request cannot reuse this disposed
            // application or render context. Successful scopes are tracked by the adaptor [SSR-9].
            Exception terminalFailure = failure;
            try
            {
                ServerRenderRequestIsolation.Track(scope.Application, scope.RenderContext);
            }
            catch (Exception isolationFailure)
            {
                terminalFailure = new AggregateException(terminalFailure, isolationFailure);
            }

            try
            {
                await scope.DisposeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                terminalFailure = new AggregateException(terminalFailure, cleanupFailure);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (failure is OperationCanceledException)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            ExceptionDispatchInfo.Capture(terminalFailure).Throw();
            throw;
        }
    }
}
