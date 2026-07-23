using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Stores and invokes the lifecycle callbacks owned by one mounted component.</summary>
internal sealed class ComponentLifecycle : IComponentLifecycle, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private List<ComponentLifecycleCallback>? _beforeMount;
    private List<ComponentLifecycleCallback>? _mounted;
    private List<ComponentLifecycleCallback>? _beforeUpdate;
    private List<ComponentLifecycleCallback>? _updated;
    private List<ComponentLifecycleCallback>? _beforeUnmount;
    private List<ComponentLifecycleCallback>? _unmounted;
    private List<ComponentLifecycleCallback>? _serverPrefetch;
    private List<ComponentLifecycleCallback>? _activated;
    private List<ComponentLifecycleCallback>? _deactivated;
    private List<Func<Exception, IComponentContext?, string, bool>>? _errorCaptured;
    private bool _isDisposed;

    /// <inheritdoc/>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <inheritdoc/>
    public void OnBeforeMount(Action callback) => Add(ref _beforeMount, callback);

    /// <inheritdoc/>
    public void OnBeforeMount(Func<Task> callback) => Add(ref _beforeMount, callback);

    /// <inheritdoc/>
    public void OnBeforeMount(Func<CancellationToken, Task> callback) =>
        Add(ref _beforeMount, callback);

    /// <inheritdoc/>
    public void OnMounted(Action callback) => Add(ref _mounted, callback);

    /// <inheritdoc/>
    public void OnMounted(Func<Task> callback) => Add(ref _mounted, callback);

    /// <inheritdoc/>
    public void OnMounted(Func<CancellationToken, Task> callback) => Add(ref _mounted, callback);

    /// <inheritdoc/>
    public void OnBeforeUpdate(Action callback) => Add(ref _beforeUpdate, callback);

    /// <inheritdoc/>
    public void OnBeforeUpdate(Func<Task> callback) => Add(ref _beforeUpdate, callback);

    /// <inheritdoc/>
    public void OnBeforeUpdate(Func<CancellationToken, Task> callback) =>
        Add(ref _beforeUpdate, callback);

    /// <inheritdoc/>
    public void OnUpdated(Action callback) => Add(ref _updated, callback);

    /// <inheritdoc/>
    public void OnUpdated(Func<Task> callback) => Add(ref _updated, callback);

    /// <inheritdoc/>
    public void OnUpdated(Func<CancellationToken, Task> callback) => Add(ref _updated, callback);

    /// <inheritdoc/>
    public void OnBeforeUnmount(Action callback) => Add(ref _beforeUnmount, callback);

    /// <inheritdoc/>
    public void OnBeforeUnmount(Func<Task> callback) => Add(ref _beforeUnmount, callback);

    /// <inheritdoc/>
    public void OnBeforeUnmount(Func<CancellationToken, Task> callback) =>
        Add(ref _beforeUnmount, callback);

    /// <inheritdoc/>
    public void OnUnmounted(Action callback) => Add(ref _unmounted, callback);

    /// <inheritdoc/>
    public void OnUnmounted(Func<Task> callback) => Add(ref _unmounted, callback);

    /// <inheritdoc/>
    public void OnUnmounted(Func<CancellationToken, Task> callback) =>
        Add(ref _unmounted, callback);

    /// <inheritdoc/>
    public void OnErrorCaptured(
        Func<Exception, IComponentContext?, string, bool> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (_errorCaptured ??= []).Add(callback);
    }

    /// <inheritdoc/>
    public void OnServerPrefetch(Func<Task> callback) => Add(ref _serverPrefetch, callback);

    /// <inheritdoc/>
    public void OnServerPrefetch(Func<CancellationToken, Task> callback) =>
        Add(ref _serverPrefetch, callback);

    /// <inheritdoc/>
    public void OnActivated(Action callback) => Add(ref _activated, callback);

    /// <inheritdoc/>
    public void OnActivated(Func<Task> callback) => Add(ref _activated, callback);

    /// <inheritdoc/>
    public void OnActivated(Func<CancellationToken, Task> callback) =>
        Add(ref _activated, callback);

    /// <inheritdoc/>
    public void OnDeactivated(Action callback) => Add(ref _deactivated, callback);

    /// <inheritdoc/>
    public void OnDeactivated(Func<Task> callback) => Add(ref _deactivated, callback);

    /// <inheritdoc/>
    public void OnDeactivated(Func<CancellationToken, Task> callback) =>
        Add(ref _deactivated, callback);

    internal void InvokeBeforeMount(ComponentContext context) =>
        Invoke(_beforeMount, context, "before-mount lifecycle callback");

    internal void InvokeMounted(ComponentContext context) =>
        Invoke(_mounted, context, "mounted lifecycle callback");

    internal void InvokeBeforeUpdate(ComponentContext context) =>
        Invoke(_beforeUpdate, context, "before-update lifecycle callback");

    internal void InvokeUpdated(ComponentContext context) =>
        Invoke(_updated, context, "updated lifecycle callback");

    internal void InvokeBeforeUnmount(ComponentContext context) =>
        Invoke(_beforeUnmount, context, "before-unmount lifecycle callback");

    internal void InvokeUnmounted(ComponentContext context) =>
        Invoke(_unmounted, context, "unmounted lifecycle callback");

    internal void InvokeActivated(ComponentContext context) =>
        Invoke(_activated, context, "activated lifecycle callback");

    internal void InvokeDeactivated(ComponentContext context) =>
        Invoke(_deactivated, context, "deactivated lifecycle callback");

    internal async Task InvokeServerPrefetchAsync(ComponentContext context)
    {
        if (_serverPrefetch is null)
        {
            return;
        }

        foreach (ComponentLifecycleCallback callback in _serverPrefetch)
        {
            await callback.InvokeAndAwaitAsync(
                context,
                "server-prefetch lifecycle callback").ConfigureAwait(false);
        }
    }

    internal bool Capture(
        ComponentContext context,
        Exception exception,
        IComponentContext? source,
        string diagnosticInformation)
    {
        if (_isDisposed || _errorCaptured is null)
        {
            return true;
        }

        foreach (Func<Exception, IComponentContext?, string, bool> callback in _errorCaptured)
        {
            bool shouldContinue = context.Run(
                () => callback(exception, source, diagnosticInformation));
            if (!shouldContinue)
            {
                return false;
            }
        }

        return true;
    }

    internal void Cancel()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }
    }

    /// <summary>Releases the component-lifetime cancellation source.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Cancel();
        _cancellation.Dispose();
    }

    private void Add(ref List<ComponentLifecycleCallback>? callbacks, Action callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new ComponentLifecycleCallback(callback));
    }

    private void Add(ref List<ComponentLifecycleCallback>? callbacks, Func<Task> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new ComponentLifecycleCallback(callback));
    }

    private void Add(
        ref List<ComponentLifecycleCallback>? callbacks,
        Func<CancellationToken, Task> callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        (callbacks ??= []).Add(new ComponentLifecycleCallback(callback));
    }

    private static void Invoke(
        List<ComponentLifecycleCallback>? callbacks,
        ComponentContext context,
        string diagnosticInformation)
    {
        if (callbacks is null)
        {
            return;
        }

        foreach (ComponentLifecycleCallback callback in callbacks)
        {
            callback.Invoke(context, diagnosticInformation);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
