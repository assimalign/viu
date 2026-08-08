using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Executes authored components over the Components-owned model: resolve, activate, set up,
/// prefetch, and render exactly once per operation.
/// </summary>
public sealed class ComponentHost
{
    private readonly ComponentRuntimeOptions _options;

    /// <summary>Initializes the component host with borrowed application composition.</summary>
    /// <param name="options">The explicit composition options.</param>
    public ComponentHost(ComponentRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    internal ComponentActivation ActivatePersistent(
        ComponentNode component,
        RuntimeComponentContext? parent,
        IReactiveWatchScheduler watchScheduler,
        SuspenseBoundary? suspenseBoundary)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(watchScheduler);
        return ComponentActivation.Activate(
            component,
            _options,
            parent,
            watchScheduler,
            suspenseBoundary: suspenseBoundary);
    }

    /// <summary>
    /// Activates, sets up, prefetches, and renders one component, returning the operation scope
    /// that keeps the component's reactive lifetime alive while its tree is consumed. The render
    /// runs against a <see cref="ComponentRenderFrame"/> constructed once per mount and kept on
    /// the returned scope's lease, so repeated renders of the mount reuse the same frame — there
    /// is no ambient render-helper state. Disposing or aborting the operation cancels the
    /// component-lifetime token before stopping its effect scope, drains observed lifecycle
    /// tasks, and never invokes client lifecycle phases. Specified by <c>[CMP-21]</c>,
    /// <c>[CMP-22]</c>, <c>[SSR-5]</c>, and <c>[SSR-10]</c>.
    /// </summary>
    /// <param name="request">The immutable invocation and optional parent operation scope.</param>
    /// <param name="cancellationToken">Cancellation for setup-adjacent asynchronous work.</param>
    /// <returns>The disposable render scope carrying the tree and live context.</returns>
    public async ValueTask<IComponentRenderScope> RenderAsync(
        ComponentRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ComponentActivation activation = await ComponentActivation.ActivateAsync(
            request.Component,
            _options,
            request.Parent?.Context).ConfigureAwait(false);

        try
        {
            Task prefetch = activation.Context.Run(
                () => activation.Lifecycle.InvokeServerPrefetchAsync(cancellationToken));
            await prefetch.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            VirtualNode? tree = activation.Render();
            return new ComponentRenderLease(activation, tree);
        }
        catch (Exception exception)
        {
            ExceptionDispatchInfo failure = ExceptionDispatchInfo.Capture(exception);
            try
            {
                await activation.ReleaseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Preserve the render or prefetch failure after completing best-effort teardown.
            }

            failure.Throw();
            throw;
        }
    }
}
