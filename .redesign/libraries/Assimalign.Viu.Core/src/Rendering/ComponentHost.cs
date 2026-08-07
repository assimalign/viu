using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Activates, sets up, prefetches, and renders one component, returning the operation scope
    /// that keeps the component's reactive lifetime alive while its tree is consumed. The render
    /// runs against a <see cref="ComponentRenderFrame"/> constructed once per mount and kept on
    /// the returned scope's lease, so repeated renders of the mount reuse the same frame — there
    /// is no ambient render-helper state. Disposing or aborting the operation cancels the
    /// component-lifetime token before stopping its effect scope, drains observed lifecycle
    /// tasks, and never invokes client lifecycle phases. Specified by <c>[CMP-21]</c>,
    /// <c>[CMP-22]</c>, and <c>[SSR-5]</c>.
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

        var registration = _options.Components.Resolve(request.Component.Component);
        var instance = registration.Activator(_options.Services);
        ArgumentNullException.ThrowIfNull(instance);
        var scope = new EffectScope();
        var lifecycle = new ComponentLifecycle();
        var diagnostics = new List<ComponentBindingDiagnostic>();
        var bindings = ComponentBindings.Resolve(
            registration.Contract,
            request.Component.Invocation,
            diagnostics);
        var context = new RuntimeComponentContext(
            bindings,
            _options.Services,
            lifecycle,
            request.Component.Invocation.Listeners,
            scope,
            _options.WatchScheduler,
            request.Parent?.Context,
            _options.ErrorHandler);
        lifecycle.SetObservedTaskFaultHandler(context.RouteError);

        try
        {
            foreach (var diagnostic in diagnostics)
            {
                context.Warn(diagnostic.Message);
            }

            var renderer = scope.Run(() => instance.Setup(context));
            ArgumentNullException.ThrowIfNull(renderer);
            await lifecycle.InvokeServerPrefetchAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new ComponentRenderFrame();
            var tree = renderer(frame);
            return new ComponentRenderLease(instance, context, tree, scope, frame, lifecycle);
        }
        catch
        {
            await ComponentRenderLease.ReleaseAsync(instance, scope, lifecycle).ConfigureAwait(false);
            throw;
        }
    }
}
