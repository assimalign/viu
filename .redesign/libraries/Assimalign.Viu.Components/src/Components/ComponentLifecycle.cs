using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>
/// Registers lifecycle callbacks and exposes the cancellation token owned by one mounted
/// instance. Constructed by the runtime, one per mount.
/// </summary>
/// <remarks>
/// The shipping surface carries synchronous, <see cref="Task"/>, and token-receiving overload
/// triples per phase with observed-task fault routing (<c>[CMP-20]</c>–<c>[CMP-22]</c>); the
/// contract model shows one representative overload per phase. Invocation members exist only for
/// the runtime and are hidden from completion — the deliberate cost of the lifecycle living in
/// the component-model assembly while the engine drives it.
/// </remarks>
public sealed class ComponentLifecycle
{
    private readonly List<(string Phase, Delegate Callback)> _registrations = [];

    /// <summary>Initializes a per-mount lifecycle. Runtime-constructed.</summary>
    public ComponentLifecycle()
    {
    }

    /// <summary>Gets the token canceled before the instance's subtree is torn down.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Registers a callback that runs before the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeMount(Action callback) => _registrations.Add((nameof(OnBeforeMount), callback));

    /// <summary>Registers a callback that runs after the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnMounted(Action callback) => _registrations.Add((nameof(OnMounted), callback));

    /// <summary>Registers a callback that runs before a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeUpdate(Action callback) => _registrations.Add((nameof(OnBeforeUpdate), callback));

    /// <summary>Registers a callback that runs after a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnUpdated(Action callback) => _registrations.Add((nameof(OnUpdated), callback));

    /// <summary>Registers a callback that runs before teardown starts.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeUnmount(Action callback) => _registrations.Add((nameof(OnBeforeUnmount), callback));

    /// <summary>Registers a callback that runs after teardown completes.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnUnmounted(Action callback) => _registrations.Add((nameof(OnUnmounted), callback));

    /// <summary>Registers a callback that runs when a cached subtree is reactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnActivated(Action callback) => _registrations.Add((nameof(OnActivated), callback));

    /// <summary>Registers a callback that runs when a cached subtree is deactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnDeactivated(Action callback) => _registrations.Add((nameof(OnDeactivated), callback));

    /// <summary>Registers a callback that captures a descendant error.</summary>
    /// <param name="callback">
    /// The callback receiving the exception, source context when available, and diagnostic
    /// information; returning false stops propagation.
    /// </param>
    public void OnErrorCaptured(Func<Exception, ComponentContext?, string, bool> callback)
        => _registrations.Add((nameof(OnErrorCaptured), callback));

    /// <summary>Registers a task server-side rendering awaits before serializing.</summary>
    /// <param name="callback">The server-prefetch task factory receiving the lifetime token.</param>
    public void OnServerPrefetch(Func<CancellationToken, Task> callback)
        => _registrations.Add((nameof(OnServerPrefetch), callback));

    /// <summary>Runtime ABI: invokes mounted callbacks. Not for application code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeMounted()
    {
    }

    /// <summary>Runtime ABI: awaits registered server-prefetch tasks. Not for application code.</summary>
    /// <param name="cancellationToken">The server-render cancellation token.</param>
    /// <returns>A task completing when every registered prefetch completes.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task InvokeServerPrefetchAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
