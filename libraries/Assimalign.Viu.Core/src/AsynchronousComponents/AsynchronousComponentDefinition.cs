using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Couples an asynchronous wrapper's explicit factory registration with render-tree requests for
/// that registration.
/// </summary>
/// <remarks>
/// The definition owns shared loader state, but it does not own the application component factory
/// or service provider. Concurrent mounts share one load, a successful target is cached, and every
/// mount still receives a fresh factory-created wrapper and resolved template. This type is not
/// thread-safe; Viu runs it on the host's single-threaded event loop.
/// </remarks>
public sealed class AsynchronousComponentDefinition
{
    private readonly AsynchronousComponentOptions _options;
    private AsynchronousComponentLoadState? _pendingLoad;
    private AsynchronousComponentTarget _resolvedTarget;
    private bool _hasResolvedTarget;

    internal AsynchronousComponentDefinition(
        Type componentType,
        AsynchronousComponentOptions options,
        string? name)
    {
        ComponentType = componentType;
        _options = options;
        Registration = new ComponentRegistration(
            componentType,
            () => new AsynchronousComponentTemplate(this),
            name);
    }

    /// <summary>Gets the stable type identity registered for this wrapper.</summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the explicit registration to include in the application-owned component factory.
    /// </summary>
    /// <remarks>
    /// The built-in factory consumes this value directly. A custom factory may map its type or
    /// name to its activator without otherwise depending on the built-in factory.
    /// </remarks>
    public ComponentRegistration Registration { get; }

    /// <summary>Creates an immutable request to mount this asynchronous component.</summary>
    /// <param name="arguments">The arguments forwarded to the resolved template.</param>
    /// <param name="slots">The slots forwarded to the resolved template.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The event listeners forwarded to the resolved template.</param>
    /// <param name="directives">The root directives forwarded to the resolved template.</param>
    /// <param name="reference">
    /// The reference forwarded to the resolved template. It remains unset while a loading, error,
    /// or empty presentation is active.
    /// </param>
    /// <returns>A template request resolved through the application component factory.</returns>
    public ITemplateComponent CreateComponent(
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        return new TemplateComponent(
            ComponentType,
            arguments,
            slots,
            key,
            optimization,
            listeners,
            directives,
            reference);
    }

    internal AsynchronousComponentOptions Options => _options;

    internal AsynchronousComponentLoadLease AcquireLoad()
    {
        if (_hasResolvedTarget)
        {
            return new AsynchronousComponentLoadLease(
                Task.FromResult(_resolvedTarget));
        }

        AsynchronousComponentLoadState state = _pendingLoad
            ?? StartLoad();
        state.ConsumerCount++;
        return new AsynchronousComponentLoadLease(
            state.PendingLoad,
            () => ReleaseLoad(state));
    }

    private AsynchronousComponentLoadState StartLoad()
    {
        AsynchronousComponentLoadState state = new();
        _pendingLoad = state;
        state.PendingLoad = LoadAsync(state);
        return state;
    }

    private void ReleaseLoad(AsynchronousComponentLoadState state)
    {
        if (state.ConsumerCount == 0)
        {
            return;
        }

        state.ConsumerCount--;
        if (state.ConsumerCount != 0 || state.PendingLoad.IsCompleted)
        {
            return;
        }

        if (ReferenceEquals(_pendingLoad, state))
        {
            _pendingLoad = null;
        }

        state.Cancellation.Cancel();
    }

    private async Task<AsynchronousComponentTarget> LoadAsync(
        AsynchronousComponentLoadState state)
    {
        try
        {
            int attempts = 0;
            while (true)
            {
                attempts++;
                try
                {
                    AsynchronousComponentTarget target =
                        await _options.Loader(state.Cancellation.Token);
                    target.Validate();
                    if (ReferenceEquals(_pendingLoad, state))
                    {
                        _resolvedTarget = target;
                        _hasResolvedTarget = true;
                    }

                    return target;
                }
                catch (OperationCanceledException)
                    when (state.Cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception error) when (_options.OnError is not null)
                {
                    bool retry = await GetRetryDecisionAsync(
                        error,
                        attempts,
                        state.Cancellation.Token);
                    if (!retry)
                    {
                        ExceptionDispatchInfo.Capture(error).Throw();
                    }
                }
            }
        }
        finally
        {
            if (ReferenceEquals(_pendingLoad, state))
            {
                _pendingLoad = null;
            }

            state.Cancellation.Dispose();
        }
    }

    private async Task<bool> GetRetryDecisionAsync(
        Exception error,
        int attempts,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> completion = new();
        using CancellationTokenRegistration registration =
            cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
        _options.OnError!(
            error,
            () => completion.TrySetResult(true),
            () => completion.TrySetResult(false),
            attempts);
        return await completion.Task;
    }
}
