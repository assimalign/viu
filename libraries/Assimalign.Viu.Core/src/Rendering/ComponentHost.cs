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
        ComponentActivation activation = await ActivateForServerAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        try
        {
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
                // Preserve the render failure after completing best-effort teardown.
            }

            failure.Throw();
            throw;
        }
    }

    /// <summary>
    /// Activates and prefetches one component, invokes <paramref name="operation"/> exactly once
    /// with its instance, frame, and live public scope, then releases the component lifetime.
    /// </summary>
    /// <param name="request">The immutable invocation and optional parent operation scope.</param>
    /// <param name="operation">The host-specific operation to execute after server prefetch.</param>
    /// <param name="cancellationToken">Cancellation for setup-adjacent asynchronous work.</param>
    /// <returns>
    /// <see cref="ComponentRenderOperationOutcome.Succeeded"/> when the operation completes, or
    /// <see cref="ComponentRenderOperationOutcome.HandledFailure"/> when the component error chain
    /// handles its failure. An unhandled failure propagates unchanged.
    /// </returns>
    /// <remarks>
    /// The callback shape keeps activation, error routing, and lifetime ownership inside Core. The
    /// supplied scope is valid only until the callback completes; callers must not retain it.
    /// Disposing the operation cancels the component-lifetime token before stopping its effect
    /// scope and never invokes client lifecycle phases. Specified by <c>[SSR-10]</c> and
    /// <c>[SSR-TARGET-3]</c>.
    /// </remarks>
    public async ValueTask<ComponentRenderOperationOutcome> ExecuteAsync(
        ComponentRenderRequest request,
        ComponentRenderOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        ComponentActivation activation = await ActivateForServerAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        await using ComponentRenderLease scope = new(activation, tree: null);
        try
        {
            await operation(scope.Instance, scope.Frame, scope).ConfigureAwait(false);
            return ComponentRenderOperationOutcome.Succeeded;
        }
        catch (Exception exception)
        {
            activation.Context.RouteError(exception, "component render");
            return ComponentRenderOperationOutcome.HandledFailure;
        }
    }

    // Shared activation keeps traversal and direct-render operations on the exact same setup,
    // prefetch, cancellation, and teardown contract without exposing activation state publicly.
    internal async ValueTask<ComponentActivation> ActivateForServerAsync(
        ComponentRenderRequest request,
        CancellationToken cancellationToken)
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
            return activation;
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
                // Preserve the activation or prefetch failure after best-effort teardown.
            }

            failure.Throw();
            throw;
        }
    }
}
