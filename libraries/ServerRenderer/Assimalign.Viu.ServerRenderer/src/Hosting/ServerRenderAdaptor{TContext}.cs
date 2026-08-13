using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Executes request-scoped server rendering over a framework-neutral factory and streaming output.
/// </summary>
/// <typeparam name="TContext">The host-defined request context type.</typeparam>
/// <remarks>
/// Every invocation obtains and disposes a new request scope. Reusing either a
/// <see cref="ServerRenderApplication"/> or <see cref="SsrContext"/> is rejected, which prevents
/// application, reactive-service, teleport, and hydration-state bleed between requests. Ordinary
/// failures are returned for host mapping; request cancellation propagates. The renderer also
/// selects fresh logical Core, Reactivity, State, and scheduler bookkeeping for every call.
/// Specified by <c>[EXE-1]</c>, <c>[SSR-1]</c>, and <c>[SSR-8]</c> through <c>[SSR-12]</c>.
/// </remarks>
public sealed class ServerRenderAdaptor<TContext>
    where TContext : notnull
{
    private readonly IServerRenderRequestScopeFactory<TContext> _requestScopeFactory;
    private readonly IServerRenderRegistry? _serverRenders;

    /// <summary>Initializes the adaptor with the host's request-scope factory.</summary>
    /// <param name="requestScopeFactory">The factory invoked exactly once per render request.</param>
    public ServerRenderAdaptor(IServerRenderRequestScopeFactory<TContext> requestScopeFactory)
    {
        ArgumentNullException.ThrowIfNull(requestScopeFactory);
        _requestScopeFactory = requestScopeFactory;
    }

    /// <summary>
    /// Initializes the adaptor with the host's request-scope factory and generated server catalog.
    /// </summary>
    /// <param name="requestScopeFactory">The factory invoked exactly once per render request.</param>
    /// <param name="serverRenders">
    /// The registry populated by the consumer assembly's generated server-registration surface.
    /// </param>
    /// <remarks>
    /// Supplying the catalog selects compiler-produced bodies for registered component subtrees and
    /// preserves traversal for unregistered values. The adaptor performs no assembly discovery.
    /// Specified by <c>[SSR-TARGET-2]</c> and <c>[SSR-TARGET-3]</c>.
    /// </remarks>
    public ServerRenderAdaptor(
        IServerRenderRequestScopeFactory<TContext> requestScopeFactory,
        IServerRenderRegistry serverRenders)
    {
        ArgumentNullException.ThrowIfNull(requestScopeFactory);
        ArgumentNullException.ThrowIfNull(serverRenders);
        _requestScopeFactory = requestScopeFactory;
        _serverRenders = serverRenders;
    }

    /// <summary>
    /// Creates a fresh request scope, streams the render, and disposes the scope before reporting
    /// the outcome to the host.
    /// </summary>
    /// <param name="request">The root component and host-defined request context.</param>
    /// <param name="output">The borrowed host response output.</param>
    /// <param name="cancellationToken">Cancellation propagated from the host request.</param>
    /// <returns>
    /// A completion or failure result the host maps to its own response policy. Cancellation throws
    /// instead of becoming a failure result.
    /// </returns>
    public async ValueTask<ServerRenderResult> RenderAsync(
        ServerRenderRequest<TContext> request,
        IServerRenderOutput output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();

        IServerRenderRequestScope? scope = null;
        SsrContext? context = null;
        ServerRenderOutputTextWriter? writer = null;
        Exception? failure = null;
        ExceptionDispatchInfo? cancellation = null;

        try
        {
            scope = await _requestScopeFactory.CreateAsync(
                request,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "The server render request-scope factory returned null.");

            ServerRenderApplication application = scope.Application
                ?? throw new InvalidOperationException(
                    "The server render request scope returned a null application.");
            context = scope.RenderContext
                ?? throw new InvalidOperationException(
                    "The server render request scope returned a null render context.");

            ServerRenderRequestIsolation.Track(application, context);

            if (!ReferenceEquals(application.Context.RootComponent, request.RootComponent))
            {
                throw new InvalidOperationException(
                    "The request-scope application must use the request's root component instance.");
            }

            writer = new ServerRenderOutputTextWriter(output);
            if (_serverRenders is null)
            {
                await ServerRenderer.RenderToStreamAsync(
                    application,
                    writer,
                    context,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ServerRenderer.RenderToStreamAsync(
                    application,
                    writer,
                    _serverRenders,
                    context,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellation = ExceptionDispatchInfo.Capture(exception);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (scope is not null)
            {
                try
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                    when (cancellationToken.IsCancellationRequested)
                {
                    cancellation ??= ExceptionDispatchInfo.Capture(exception);
                }
                catch (Exception exception)
                {
                    failure = failure is null
                        ? exception
                        : new AggregateException(failure, exception);
                }
            }
        }

        cancellation?.Throw();
        cancellationToken.ThrowIfCancellationRequested();
        return new ServerRenderResult(
            context,
            failure,
            writer?.ResponseStarted ?? false);
    }

}
