using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Couples an asynchronous wrapper's explicit registration with requests for that registration.
/// </summary>
/// <remarks>
/// Concurrent mounts share one load and a successful target is cached, while activation still
/// produces a fresh wrapper for every mount. This type is not thread-safe; Viu drives it on the
/// host event loop. Specified by <c>[BLT-14]</c>.
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
        ComponentReference typeReference = ComponentReference.ForType(componentType);
        ComponentType = componentType;
        Reference = name is null
            ? typeReference
            : ComponentReference.ForName(name);
        _options = options;
        Registration = new ComponentRegistration(
            Reference,
            new ComponentContract(
                displayName: "AsynchronousComponent",
                flags: ComponentFlags.None),
            _ => new AsynchronousComponentWrapper(this));
    }

    /// <summary>Gets the stable authored type identity associated with this wrapper.</summary>
    public Type ComponentType { get; }

    /// <summary>Gets the reference carried by requests for this definition.</summary>
    public ComponentReference Reference { get; }

    /// <summary>Gets the explicit registration to add to the application component factory.</summary>
    public ComponentRegistration Registration { get; }

    /// <summary>Creates an immutable node that requests this asynchronous component.</summary>
    /// <param name="invocation">The arguments, slots, listeners, and directives to forward.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="mountReference">The optional exposed-value receiver.</param>
    /// <param name="renderPlan">The compiler patch information.</param>
    /// <returns>A component node resolved through <see cref="Registration"/>.</returns>
    public ComponentNode CreateComponent(
        ComponentInvocation? invocation = null,
        object? key = null,
        MountReference? mountReference = null,
        RenderPlan? renderPlan = null)
    {
        return new ComponentNode(
            Reference,
            invocation,
            key,
            mountReference,
            renderPlan);
    }

    internal AsynchronousComponentOptions Options => _options;

    internal AsynchronousComponentLoadLease AcquireLoad()
    {
        if (_hasResolvedTarget)
        {
            return new AsynchronousComponentLoadLease(Task.FromResult(_resolvedTarget));
        }

        AsynchronousComponentLoadState state = _pendingLoad ?? StartLoad();
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
                        await _options.Loader(state.Cancellation.Token).ConfigureAwait(false);
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
                        state.Cancellation.Token).ConfigureAwait(false);
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
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        _options.OnError!(
            error,
            () => completion.TrySetResult(true),
            () => completion.TrySetResult(false),
            attempts);
        return await completion.Task.ConfigureAwait(false);
    }
}
