using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Tracks the pending asynchronous dependencies owned by one Suspense mount.</summary>
internal sealed class SuspenseBoundary : ISuspenseBoundary, IDisposable
{
    private readonly Dictionary<Task, int> _dependencies =
        new(ReferenceEqualityComparer.Instance);
    private Action? _scheduleUpdate;
    private bool _isDisposed;

    internal bool IsPending => _dependencies.Count > 0;

    internal int PendingDependencyCount => _dependencies.Count;

    internal void BindUpdateScheduler(Action scheduleUpdate)
    {
        ArgumentNullException.ThrowIfNull(scheduleUpdate);
        _scheduleUpdate = scheduleUpdate;
    }

    /// <inheritdoc/>
    public void RegisterAsynchronousDependency(
        IComponentContext component,
        Task pendingLoad)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(pendingLoad);
        if (_isDisposed || pendingLoad.IsCompleted)
        {
            return;
        }

        component.Lifecycle.OnBeforeUnmount(
            () => ReleaseDependency(pendingLoad));
        if (_dependencies.TryGetValue(
            pendingLoad,
            out int consumerCount))
        {
            _dependencies[pendingLoad] = checked(consumerCount + 1);
        }
        else
        {
            _dependencies.Add(pendingLoad, 1);
            _scheduleUpdate?.Invoke();
            _ = ObserveDependencyAsync(pendingLoad);
        }
    }

    private async Task ObserveDependencyAsync(Task pendingLoad)
    {
        try
        {
            await pendingLoad;
        }
        catch (Exception)
        {
            // The mounted asynchronous wrapper owns error and cancellation routing through its
            // component context. Suspense only treats either outcome as a settled dependency.
        }
        finally
        {
            if (!_isDisposed && _dependencies.Remove(pendingLoad))
            {
                _scheduleUpdate?.Invoke();
            }
        }
    }

    private void ReleaseDependency(Task pendingLoad)
    {
        if (_isDisposed
            || !_dependencies.TryGetValue(
                pendingLoad,
                out int consumerCount))
        {
            return;
        }

        if (consumerCount > 1)
        {
            _dependencies[pendingLoad] = consumerCount - 1;
            return;
        }

        _dependencies.Remove(pendingLoad);
        _scheduleUpdate?.Invoke();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _scheduleUpdate = null;
        _dependencies.Clear();
    }
}
