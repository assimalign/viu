using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Renders an explicit route list into complete, hydratable static documents.</summary>
/// <remarks>Routes execute sequentially with fresh application, context, and memory-history identities. The generator borrows factories, shell, output, and registrations. It is not thread-safe. Specified by <c>[SSG-1]</c> through <c>[SSG-5]</c>.</remarks>
public sealed class StaticSiteGenerator
{
    private readonly Func<VirtualNode> _rootFactory;
    private readonly IServerRenderRequestScopeFactory<StaticSiteRouteContext> _requestScopeFactory;
    private readonly Func<string, IRouterHistory> _historyFactory;
    private readonly IServerRenderRegistry? _serverRenders;
    private bool _generating;

    /// <summary>Configures explicit root activation, request ownership, and optional compiled rendering.</summary>
    /// <param name="rootFactory">Creates the root description passed to each request factory.</param>
    /// <param name="requestScopeFactory">Creates a fresh scope whose application uses the requested root. A routed scope exposes its Router through application services and owns its disposal.</param>
    /// <param name="historyFactory">Creates a fresh host-free memory history for the supplied base; null uses RouterHistory.CreateMemory.</param>
    /// <param name="serverRenders">Optional explicit generated server catalog; no assembly discovery occurs.</param>
    /// <remarks>Factories must clean up resources when they fail before returning ownership. Specified by <c>[SSG-1]</c> and <c>[SSR-9]</c>.</remarks>
    public StaticSiteGenerator(
        Func<VirtualNode> rootFactory,
        IServerRenderRequestScopeFactory<StaticSiteRouteContext> requestScopeFactory,
        Func<string, IRouterHistory>? historyFactory = null,
        IServerRenderRegistry? serverRenders = null)
    {
        ArgumentNullException.ThrowIfNull(rootFactory);
        ArgumentNullException.ThrowIfNull(requestScopeFactory);
        _rootFactory = rootFactory;
        _requestScopeFactory = requestScopeFactory;
        _historyFactory = historyFactory ?? (basePath => RouterHistory.CreateMemory(basePath));
        _serverRenders = serverRenders;
    }

    /// <summary>Emits routes in input order, stopping at the first failure.</summary>
    /// <param name="routes">Base-stripped locations; query and fragment participate in routing only.</param>
    /// <param name="documentShell">The borrowed document prefix/suffix, normally a published host page.</param>
    /// <param name="output">The borrowed destination for complete UTF-8 documents.</param>
    /// <param name="basePath">The deployment base used by memory-history links, never prepended to output paths.</param>
    /// <param name="fileEmitted">Optional synchronous report after each successful write.</param>
    /// <param name="cancellationToken">Cancellation propagated through routing, rendering, teardown, and output.</param>
    /// <returns>Every emitted file, in route order.</returns>
    /// <exception cref="StaticSiteGenerationException">A route failed; its name and original cause are retained.</exception>
    /// <remarks>No incomplete render reaches output. Already emitted files remain after a later failure. Request cancellation propagates unchanged. Specified by <c>[SSG-2]</c>, <c>[SSG-4]</c>, and <c>[SSG-5]</c>.</remarks>
    public async Task<IReadOnlyList<StaticSiteFile>> GenerateAsync(
        IEnumerable<string> routes,
        IServerRenderDocumentShell documentShell,
        IStaticSiteOutput output,
        string basePath = "/",
        Action<StaticSiteFile>? fileEmitted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(documentShell);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        _ = StaticSitePath.GetRelativePath(basePath);
        if (basePath.Contains('?') || basePath.Contains('#'))
        {
            throw new ArgumentException("The deployment base cannot contain a query or fragment.", nameof(basePath));
        }

        if (_generating)
        {
            throw new InvalidOperationException("A static site generator cannot run overlapping generations.");
        }

        _generating = true;
        List<StaticSiteFile> files = [];
        Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string route in routes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using IDisposable execution = RuntimeExecution.EnterExecutionFlow();
                    string relativePath = StaticSitePath.GetRelativePath(route);
                    if (!paths.TryAdd(relativePath, route))
                    {
                        throw new InvalidOperationException($"Routes '{paths[relativePath]}' and '{route}' map to the same document '{relativePath}'.");
                    }

                    StaticSiteRenderOutput buffer = new();
                    await RenderRouteAsync(route, basePath, documentShell, buffer, cancellationToken)
                        .ConfigureAwait(false);

                    string location = await output.WriteAsync(relativePath, buffer.ToString(), cancellationToken).ConfigureAwait(false);
                    StaticSiteFile file = new(route, relativePath, location);
                    files.Add(file);
                    fileEmitted?.Invoke(file);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new StaticSiteGenerationException(route, exception);
                }
            }

            return files.AsReadOnly();
        }
        finally
        {
            _generating = false;
        }
    }

    private async Task RenderRouteAsync(
        string route,
        string basePath,
        IServerRenderDocumentShell documentShell,
        StaticSiteRenderOutput output,
        CancellationToken cancellationToken)
    {
        IRouterHistory history = _historyFactory(basePath)
            ?? throw new InvalidOperationException("The memory-history factory returned null.");
        Exception? failure = null;
        ExceptionDispatchInfo? cancellation = null;
        try
        {
            if (history is IInitializableRouterHistory)
            {
                throw new InvalidOperationException("Static prerendering requires a host-free memory history.");
            }

            history.Replace(route);
            StaticSiteRouteContext context = new(route, basePath, history);
            StaticSiteRequestScopeFactory factory = new(_requestScopeFactory);
            ServerRenderAdaptor<StaticSiteRouteContext> adaptor = _serverRenders is null
                ? new(factory)
                : new(factory, _serverRenders);
            ServerRenderRequest<StaticSiteRouteContext> request = new(_rootFactory(), context);
            ServerRenderResult result = await adaptor.RenderDocumentAsync(
                request, output, documentShell, cancellationToken).ConfigureAwait(false);
            failure = result.Failure;
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
            // The history outlives the adaptor-owned scope, whose Router borrows it [RTR-3].
            // Retain the original route failure when a custom history also fails teardown [SSG-5].
            try
            {
                history.Dispose();
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                cancellation ??= ExceptionDispatchInfo.Capture(exception);
            }
            catch (Exception exception)
            {
                failure = failure is null ? exception : new AggregateException(failure, exception);
            }
        }

        cancellation?.Throw();
        cancellationToken.ThrowIfCancellationRequested();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
