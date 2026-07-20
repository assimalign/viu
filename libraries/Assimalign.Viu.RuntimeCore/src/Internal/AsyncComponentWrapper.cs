using System;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The component a <see cref="AsyncComponents.DefineAsyncComponent(AsyncComponentOptions)"/> wrapper
/// runs — the C# port of upstream's <c>AsyncComponentWrapper</c>
/// (<c>packages/runtime-core/src/apiAsyncComponent.ts</c>,
/// https://vuejs.org/guide/components/async.html). One wrapper is created per
/// <c>DefineAsyncComponent</c> call, and its load state (the in-flight request, the cached resolved
/// definition, the retry count) lives as instance fields so every mount of <b>this</b> async
/// component shares one load and one cached result — upstream's <c>pendingRequest</c>/
/// <c>resolvedComp</c> closure state. Per-mount UI state (the loaded/error/delayed refs and the delay/
/// timeout timers) is closure state created fresh in each <see cref="Setup"/> instead, so two
/// simultaneous mounts each show their own loading UI while sharing the one load.
/// <para>
/// When the load settles, the reactive <c>loaded</c>/<c>error</c> refs flip and the wrapper re-renders
/// through the scheduler (no polling). A kept-alive async component forces its <see cref="KeepAlive"/>
/// parent to re-render on resolution so it caches the now-resolved subtree; because the wrapper
/// instance itself is what KeepAlive preserves, a resolved kept-alive async component keeps its state
/// and never re-invokes the loader across switches. Not thread-safe (single-threaded JS event-loop
/// model).
/// </para>
/// </summary>
internal sealed class AsyncComponentWrapper : IComponentDefinition
{
    private readonly AsyncComponentOptions _options;

    // Shared load state across every mount of this async component (upstream: defineAsyncComponent
    // closure). _pendingToken identifies the in-flight request so a retry that replaced it can be
    // reconciled (upstream: thisRequest !== pendingRequest).
    private Task<IComponentDefinition>? _pendingRequest;
    private object? _pendingToken;
    private IComponentDefinition? _resolvedComponent;
    private int _retries;

    internal AsyncComponentWrapper(AsyncComponentOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public string? Name => "AsyncComponentWrapper";

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var instance = ComponentInstance.Current!;

        // Already resolved by an earlier mount: render the inner component directly, no reload
        // (upstream: if (resolvedComp) return () => createInnerComp(resolvedComp, instance)).
        if (_resolvedComponent is { } alreadyResolved)
        {
            return () => CreateInnerComponent(alreadyResolved, instance);
        }

        var boundary = instance.SuspenseBoundary;
        var suspenseControlled = _options.Suspensible && boundary is not null;

        var loaded = Reactive.Reference(false);
        var error = Reactive.Reference<Exception?>(null);
        // delayed starts true iff a positive delay is configured (upstream: delayed = ref(!!delay));
        // a Suspense-controlled load shows no loading UI of its own, so it never delays.
        var delayed = Reactive.Reference(!suspenseControlled && _options.Delay > 0);
        IDisposable? delayTimer = null;
        IDisposable? timeoutTimer = null;

        if (!suspenseControlled)
        {
            if (_options.Delay > 0)
            {
                // After the delay, reveal the loading component — unless the load already settled
                // (upstream: setTimeout(() => { delayed.value = false }, delay)).
                delayTimer = AsyncComponentDelay.Schedule(_options.Delay, () => delayed.Value = false);
            }
            if (_options.Timeout is { } timeout)
            {
                // On timeout, settle to the error state unless already loaded/errored (upstream:
                // setTimeout(() => { if (!loaded && !error) { err = ...; onError(err); error.value = err } }, timeout)).
                timeoutTimer = AsyncComponentDelay.Schedule(timeout, () =>
                {
                    if (!loaded.Value && error.Value is null)
                    {
                        var timeoutError = new TimeoutException($"Async component timed out after {timeout}ms.");
                        HandleLoaderError(timeoutError, instance);
                        error.Value = timeoutError;
                    }
                });
            }
        }

        // Kick off (or join) the shared load. TrackLoadAsync invokes Load() synchronously before its
        // first await, so _pendingRequest is set by the time the suspense branch below reads it.
        _ = TrackLoadAsync(loaded, error, instance);

        if (suspenseControlled)
        {
            // Suspense-controlled: hand the boundary the in-flight load and let it drive the fallback
            // (upstream: the suspensible && instance.suspense arm returns the load promise for
            // Suspense to await). The render function shows the resolved subtree once loaded, else
            // nothing — the boundary owns the fallback. Real fallback display lands in [V01.01.03.20].
            boundary!.RegisterAsyncDependency(instance, _pendingRequest!);
            return () => loaded.Value && _resolvedComponent is { } resolved
                ? CreateInnerComponent(resolved, instance)
                : null;
        }

        // Discard the pending timers on unmount so a load that never resolves leaks nothing and a
        // late timer cannot touch a torn-down instance (AC: unmount before resolution is clean).
        Lifecycle.OnUnmounted(() =>
        {
            delayTimer?.Dispose();
            timeoutTimer?.Dispose();
        });

        // The render function (upstream: the returned () => { ... }). Reads the reactive refs so the
        // render effect re-runs when the load settles or the delay elapses.
        return () =>
        {
            if (loaded.Value && _resolvedComponent is { } resolved)
            {
                return CreateInnerComponent(resolved, instance);
            }
            if (error.Value is { } currentError && _options.ErrorComponent is { } errorComponent)
            {
                return VirtualNodeFactory.Component(
                    errorComponent,
                    VirtualNodeFactory.Properties(("error", currentError)));
            }
            if (_options.LoadingComponent is { } loadingComponent && !delayed.Value)
            {
                return VirtualNodeFactory.Component(loadingComponent);
            }
            return null;
        };
    }

    private async Task TrackLoadAsync(IReference<bool> loaded, IReference<Exception?> error, ComponentInstance instance)
    {
        try
        {
            // Context-capturing await (no ConfigureAwait(false)): resumes on the WASM sync context.
            await Load();
            loaded.Value = true;
            // A kept-alive async component: force the KeepAlive parent to re-render so it re-evaluates
            // and caches the now-resolved subtree (upstream: if the parent is KeepAlive, mark its
            // effect dirty and queueJob(parent.update)). Guarded so a resolve after unmount is a no-op.
            if (!instance.IsUnmounted
                && instance.Parent is { Definition: KeepAlive } keepAliveParent
                && keepAliveParent.UpdateJob is { } parentJob)
            {
                Scheduler.QueueJob(parentJob);
            }
        }
        catch (Exception loadError)
        {
            HandleLoaderError(loadError, instance);
            error.Value = loadError;
        }
    }

    private void HandleLoaderError(Exception error, ComponentInstance instance)
    {
        // Upstream onError: drop the settled request (so a later mount can retry the load) and route
        // through the error-capture chain. When an error component will display the failure it is
        // "handled" — do not crash the flush (upstream: handleError(..., throwInDev: !errorComponent));
        // with no error component, surface it (Viu's crash-loudly default for unhandled errors).
        _pendingRequest = null;
        ComponentErrorHandling.Handle(
            error, instance, "async component loader", rethrowIfUnhandled: _options.ErrorComponent is null);
    }

    private Task<IComponentDefinition> Load()
    {
        // Upstream load(): one in-flight request shared across concurrent mounts (pendingRequest).
        if (_pendingRequest is not null)
        {
            return _pendingRequest;
        }
        var token = new object();
        _pendingToken = token;
        return _pendingRequest = LoadOnceAsync(token);
    }

    private Task<IComponentDefinition> Retry()
    {
        // Upstream retry(): bump the attempt count, drop the settled request, and load again.
        _retries++;
        _pendingRequest = null;
        return Load();
    }

    private async Task<IComponentDefinition> LoadOnceAsync(object token)
    {
        IComponentDefinition component;
        try
        {
            component = await _options.Loader();
        }
        catch (Exception loadError) when (_options.OnError is not null)
        {
            // Hand the user retry/fail control (upstream: the loader().catch arm wrapping a new
            // Promise whose resolve = retry() and reject = the error).
            var completion = new TaskCompletionSource<IComponentDefinition>();
            void UserRetry() => _ = BridgeRetryAsync(completion);
            void UserFail() => completion.TrySetException(loadError);
            _options.OnError(loadError, UserRetry, UserFail, _retries + 1);
            component = await completion.Task;
        }
        // A retry issued since this request superseded it: defer to the newest request (upstream:
        // if (thisRequest !== pendingRequest && pendingRequest) return pendingRequest).
        if (!ReferenceEquals(_pendingToken, token) && _pendingRequest is not null)
        {
            return await _pendingRequest;
        }
        _resolvedComponent = component;
        return component;
    }

    private async Task BridgeRetryAsync(TaskCompletionSource<IComponentDefinition> completion)
    {
        // Resolve the onError completion with whatever the retried load settles to, so the awaiting
        // LoadOnceAsync continues (or fails) once the retry does.
        try
        {
            completion.TrySetResult(await Retry());
        }
        catch (Exception retryError)
        {
            completion.TrySetException(retryError);
        }
    }

    private static VirtualNode CreateInnerComponent(IComponentDefinition resolved, ComponentInstance instance)
    {
        // Upstream createInnerComp: createVNode(resolvedComp, parent.vnode.props, parent.vnode.children).
        // The wrapper declares no props, so its whole prop bag (including the reserved "ref", which the
        // factory re-extracts onto the inner vnode) and its slots forward to the resolved component,
        // making the wrapper transparent.
        var wrapperVNode = instance.VirtualNode;
        return VirtualNodeFactory.Component(resolved, wrapperVNode.Properties, wrapperVNode.SlotChildren);
    }
}
