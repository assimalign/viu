using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>
/// Registers lifecycle callbacks and exposes the cancellation token owned by one mounted
/// component instance.
/// </summary>
/// <remarks>
/// Ordinary asynchronous callbacks start in registration order without delaying lifecycle
/// progression. Their tasks are observed and faults are sent through the runtime-installed error
/// hook. Server-prefetch callbacks are the sole awaited phase. This type is not thread-safe; Viu
/// drives it on the host event loop. Specified by <c>[CMP-20]</c>, <c>[CMP-21]</c>,
/// <c>[CMP-22]</c>, and <c>[CMP-23]</c>.
/// </remarks>
public sealed class ComponentLifecycle : IDisposable
{
    private readonly CancellationTokenSource _cancellationSource = new();
    private readonly CancellationToken _cancellationToken;
    private List<LifecycleCallback>? _beforeMount;
    private List<LifecycleCallback>? _mounted;
    private List<LifecycleCallback>? _beforeUpdate;
    private List<LifecycleCallback>? _updated;
    private List<LifecycleCallback>? _beforeUnmount;
    private List<LifecycleCallback>? _unmounted;
    private List<LifecycleCallback>? _activated;
    private List<LifecycleCallback>? _deactivated;
    private List<LifecycleCallback>? _serverPrefetch;
    private List<Func<Exception, ComponentContext?, string, bool>>? _errorCaptured;
    private List<Task>? _taskObservers;
    private Action<Exception, string>? _observedTaskFaultHandler;
    private bool _isDisposed;

    /// <summary>Initializes a per-mount lifecycle registration surface.</summary>
    public ComponentLifecycle()
    {
        _cancellationToken = _cancellationSource.Token;
    }

    /// <summary>
    /// Gets the token cancelled after before-unmount callbacks start and before the component's
    /// effect scope and subtree are torn down. Specified by <c>[CMP-22]</c>.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Registers a synchronous callback that runs before the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeMount(Action callback) => Add(ref _beforeMount, callback);

    /// <summary>Registers an observed asynchronous callback that starts before initial mount.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnBeforeMount(Func<Task> callback) => Add(ref _beforeMount, callback);

    /// <summary>Registers an observed asynchronous callback that starts before initial mount.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnBeforeMount(Func<CancellationToken, Task> callback) => Add(ref _beforeMount, callback);

    /// <summary>Registers a synchronous callback that runs after the initial subtree is mounted.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnMounted(Action callback) => Add(ref _mounted, callback);

    /// <summary>Registers an observed asynchronous callback that starts after initial mount.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnMounted(Func<Task> callback) => Add(ref _mounted, callback);

    /// <summary>Registers an observed asynchronous callback that starts after initial mount.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnMounted(Func<CancellationToken, Task> callback) => Add(ref _mounted, callback);

    /// <summary>Registers a synchronous callback that runs before a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeUpdate(Action callback) => Add(ref _beforeUpdate, callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later patch.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnBeforeUpdate(Func<Task> callback) => Add(ref _beforeUpdate, callback);

    /// <summary>Registers an observed asynchronous callback that starts before a later patch.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnBeforeUpdate(Func<CancellationToken, Task> callback) => Add(ref _beforeUpdate, callback);

    /// <summary>Registers a synchronous callback that runs after a later subtree is patched.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnUpdated(Action callback) => Add(ref _updated, callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later patch.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnUpdated(Func<Task> callback) => Add(ref _updated, callback);

    /// <summary>Registers an observed asynchronous callback that starts after a later patch.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnUpdated(Func<CancellationToken, Task> callback) => Add(ref _updated, callback);

    /// <summary>Registers a synchronous callback that runs before teardown starts.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnBeforeUnmount(Action callback) => Add(ref _beforeUnmount, callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnBeforeUnmount(Func<Task> callback) => Add(ref _beforeUnmount, callback);

    /// <summary>Registers an observed asynchronous callback that starts before teardown.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnBeforeUnmount(Func<CancellationToken, Task> callback) => Add(ref _beforeUnmount, callback);

    /// <summary>Registers a synchronous callback that runs after teardown completes.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnUnmounted(Action callback) => Add(ref _unmounted, callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnUnmounted(Func<Task> callback) => Add(ref _unmounted, callback);

    /// <summary>Registers an observed asynchronous callback that starts after teardown.</summary>
    /// <param name="callback">
    /// The task factory receiving the already-cancelled component-lifetime token.
    /// </param>
    public void OnUnmounted(Func<CancellationToken, Task> callback) => Add(ref _unmounted, callback);

    /// <summary>Registers a synchronous callback that runs when retained state is reactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnActivated(Action callback) => Add(ref _activated, callback);

    /// <summary>Registers an observed asynchronous callback that starts on reactivation.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnActivated(Func<Task> callback) => Add(ref _activated, callback);

    /// <summary>Registers an observed asynchronous callback that starts on reactivation.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnActivated(Func<CancellationToken, Task> callback) => Add(ref _activated, callback);

    /// <summary>Registers a synchronous callback that runs when retained state is deactivated.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnDeactivated(Action callback) => Add(ref _deactivated, callback);

    /// <summary>Registers an observed asynchronous callback that starts on deactivation.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnDeactivated(Func<Task> callback) => Add(ref _deactivated, callback);

    /// <summary>Registers an observed asynchronous callback that starts on deactivation.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnDeactivated(Func<CancellationToken, Task> callback) => Add(ref _deactivated, callback);

    /// <summary>Registers a callback that may stop propagation of a descendant error.</summary>
    /// <param name="callback">
    /// The callback receiving the error, its source context when available, and diagnostic
    /// information. Returning <see langword="false"/> stops propagation.
    /// </param>
    public void OnErrorCaptured(Func<Exception, ComponentContext?, string, bool> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (_errorCaptured ??= []).Add(callback);
    }

    /// <summary>Registers synchronous setup work that server rendering runs before serialization.</summary>
    /// <param name="callback">The instance-local callback.</param>
    public void OnServerPrefetch(Action callback) => Add(ref _serverPrefetch, callback);

    /// <summary>Registers a task that server rendering awaits before serialization.</summary>
    /// <param name="callback">The instance-local task factory.</param>
    public void OnServerPrefetch(Func<Task> callback) => Add(ref _serverPrefetch, callback);

    /// <summary>Registers a task that server rendering awaits before serialization.</summary>
    /// <param name="callback">The task factory receiving the component-lifetime token.</param>
    public void OnServerPrefetch(Func<CancellationToken, Task> callback) => Add(ref _serverPrefetch, callback);

    /// <summary>
    /// Runtime operation that installs terminal routing for observed ordinary-hook faults. Each
    /// started observer retains the handler installed at invocation, including through lifecycle
    /// disposal, so a late task fault remains observed exactly once. Specified by
    /// <c>[CMP-21]</c> and <c>[CMP-23]</c>.
    /// </summary>
    /// <param name="handler">The terminal fault handler and diagnostic information receiver.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void SetObservedTaskFaultHandler(Action<Exception, string> handler)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(handler);
        _observedTaskFaultHandler = handler;
    }

    /// <summary>Runtime operation that starts callbacks registered for before-mount.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeBeforeMount() => Invoke(_beforeMount, "before-mount lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for mounted.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeMounted() => Invoke(_mounted, "mounted lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for before-update.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeBeforeUpdate() => Invoke(_beforeUpdate, "before-update lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for updated.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeUpdated() => Invoke(_updated, "updated lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for before-unmount.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeBeforeUnmount() => Invoke(_beforeUnmount, "before-unmount lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for unmounted.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeUnmounted() => Invoke(_unmounted, "unmounted lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for activated.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeActivated() => Invoke(_activated, "activated lifecycle callback");

    /// <summary>Runtime operation that starts callbacks registered for deactivated.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void InvokeDeactivated() => Invoke(_deactivated, "deactivated lifecycle callback");

    /// <summary>Runtime operation that invokes this component's error-capture chain.</summary>
    /// <param name="exception">The error being propagated.</param>
    /// <param name="source">The descendant source context when available.</param>
    /// <param name="diagnosticInformation">The operation in which the error occurred.</param>
    /// <returns><see langword="true"/> to continue toward the next ancestor.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool InvokeErrorCaptured(
        Exception exception,
        ComponentContext? source,
        string diagnosticInformation)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(diagnosticInformation);
        if (_isDisposed || _errorCaptured is null)
        {
            return true;
        }

        foreach (Func<Exception, ComponentContext?, string, bool> callback in _errorCaptured)
        {
            if (!callback(exception, source, diagnosticInformation))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Runtime operation that awaits all server-prefetch callbacks in registration order.</summary>
    /// <param name="cancellationToken">Cancellation for the server-rendering wait.</param>
    /// <returns>A task completing after every registered prefetch callback completes.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task InvokeServerPrefetchAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_serverPrefetch is null)
        {
            return;
        }

        Action<Exception, string>? faultHandler = _observedTaskFaultHandler;
        foreach (LifecycleCallback callback in _serverPrefetch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Task? task = callback.InvokeForAwait(_cancellationToken);
                if (task is null)
                {
                    throw new InvalidOperationException(
                        "An asynchronous server-prefetch lifecycle callback returned a null task.");
                }

                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                RouteFault(exception, "server-prefetch lifecycle callback", faultHandler);
            }
        }
    }

    /// <summary>Runtime operation that cancels the component-lifetime token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Cancel()
    {
        if (!_isDisposed && !_cancellationSource.IsCancellationRequested)
        {
            _cancellationSource.Cancel();
        }
    }

    /// <summary>
    /// Runtime operation that awaits every tracked ordinary-hook observer. Callback faults have
    /// already been routed and are not reported a second time by this drain.
    /// </summary>
    /// <returns>A task completing when the current observer set is empty.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task DrainAsync()
    {
        while (_taskObservers is { Count: > 0 })
        {
            Task[] observers = _taskObservers.ToArray();
            _taskObservers.Clear();
            await Task.WhenAll(observers).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runtime operation that cancels the component lifetime, releases registrations, and
    /// disposes its cancellation source. Repeated disposal is harmless.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Cancel();
        _isDisposed = true;
        _beforeMount = null;
        _mounted = null;
        _beforeUpdate = null;
        _updated = null;
        _beforeUnmount = null;
        _unmounted = null;
        _activated = null;
        _deactivated = null;
        _serverPrefetch = null;
        _errorCaptured = null;
        _observedTaskFaultHandler = null;
        _cancellationSource.Dispose();
    }

    private void Add(ref List<LifecycleCallback>? callbacks, Action callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new LifecycleCallback(callback));
    }

    private void Add(ref List<LifecycleCallback>? callbacks, Func<Task> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new LifecycleCallback(callback));
    }

    private void Add(
        ref List<LifecycleCallback>? callbacks,
        Func<CancellationToken, Task> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new LifecycleCallback(callback));
    }

    private void Invoke(List<LifecycleCallback>? callbacks, string diagnosticInformation)
    {
        ThrowIfDisposed();
        if (callbacks is null)
        {
            return;
        }

        foreach (LifecycleCallback callback in callbacks)
        {
            Task? task;
            try
            {
                task = callback.Invoke(_cancellationToken);
            }
            catch (Exception exception)
            {
                Observe(Task.FromException(exception), diagnosticInformation);
                continue;
            }

            if (callback.IsAsynchronous)
            {
                Observe(task, diagnosticInformation);
            }
        }
    }

    private void Observe(Task? task, string diagnosticInformation)
    {
        Action<Exception, string>? faultHandler = _observedTaskFaultHandler;
        Task observer = ObserveCoreAsync(task, diagnosticInformation, faultHandler);
        (_taskObservers ??= []).Add(observer);
    }

    private async Task ObserveCoreAsync(
        Task? task,
        string diagnosticInformation,
        Action<Exception, string>? faultHandler)
    {
        try
        {
            if (task is null)
            {
                throw new InvalidOperationException(
                    "An asynchronous lifecycle callback returned a null task.");
            }

            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RouteFault(exception, diagnosticInformation, faultHandler);
        }
    }

    private static void RouteFault(
        Exception exception,
        string diagnosticInformation,
        Action<Exception, string>? faultHandler)
    {
        if (faultHandler is not null)
        {
            faultHandler(exception, diagnosticInformation);
            return;
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private readonly struct LifecycleCallback
    {
        private readonly Action? _synchronousCallback;
        private readonly Func<Task>? _asynchronousCallback;
        private readonly Func<CancellationToken, Task>? _cancellableCallback;

        internal LifecycleCallback(Action callback)
        {
            _synchronousCallback = callback;
            _asynchronousCallback = null;
            _cancellableCallback = null;
        }

        internal LifecycleCallback(Func<Task> callback)
        {
            _synchronousCallback = null;
            _asynchronousCallback = callback;
            _cancellableCallback = null;
        }

        internal LifecycleCallback(Func<CancellationToken, Task> callback)
        {
            _synchronousCallback = null;
            _asynchronousCallback = null;
            _cancellableCallback = callback;
        }

        internal bool IsAsynchronous => _synchronousCallback is null;

        internal Task? Invoke(CancellationToken cancellationToken)
        {
            if (_synchronousCallback is not null)
            {
                _synchronousCallback();
                return Task.CompletedTask;
            }

            return _asynchronousCallback is not null
                ? _asynchronousCallback()
                : _cancellableCallback!(cancellationToken);
        }

        internal Task? InvokeForAwait(CancellationToken cancellationToken) =>
            Invoke(cancellationToken);
    }
}
